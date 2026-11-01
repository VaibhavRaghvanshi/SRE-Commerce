import {DatabaseSync} from 'node:sqlite';
import {createHash} from 'node:crypto';
import {readFileSync,writeFileSync,existsSync} from 'node:fs';
import {resolve,join,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
import {request,signIn} from './lib/local-commerce.mjs';

// Approved historical backfill only. No stock, Payment or messaging API is called.
const root=resolve(dirname(fileURLToPath(import.meta.url)),'..');
const source=join(root,'SRECommerce.Legacy.API/SRECommerce.Api/App_Data/srecommerce.db');
const baseline=join(root,'artifacts/baseline-20261028-232640');
const manifest=JSON.parse(readFileSync(join(baseline,'sqlite-manifest.json'),'utf8'));
const hash=path=>createHash('sha256').update(readFileSync(path)).digest('hex');
if(hash(source)!==manifest.sourceSha256)throw Error('Original source differs from the approved baseline; no import attempted.');
function readOrders(path){const db=new DatabaseSync(path,{readOnly:true});try{if(db.prepare('PRAGMA integrity_check').get().integrity_check!=='ok')throw Error('Source integrity check failed.');return{orders:db.prepare('SELECT * FROM Orders ORDER BY Id').all(),items:db.prepare('SELECT * FROM OrderItems ORDER BY Id').all()};}finally{db.close();}}
const rows=readOrders(join(baseline,'srecommerce.backup.db'));
if(JSON.stringify(rows)!==JSON.stringify(readOrders(source)))throw Error('Source and approved backup differ logically; both preserved.');
if(rows.orders.length!==3||rows.items.length!==6)throw Error('Unexpected historical source count.');
const catalog=JSON.parse(readFileSync(join(root,'artifacts/legacy-demo-catalog-mapping.json'),'utf8'));
const commerce=JSON.parse(readFileSync(join(root,'artifacts/legacy-demo-commerce-import.json'),'utf8'));
if(catalog.sourceHash!==manifest.sourceSha256||commerce.sourceHash!==manifest.sourceSha256||catalog.tenantId!==commerce.tenantId)throw Error('Source mapping mismatch.');
function cents(value){const match=String(value).match(/^(\d+)(?:\.(\d{0,4}))?$/);if(!match||((match[2]||'').slice(2).replaceAll('0','')))throw Error('Source currency precision requires review.');return BigInt(match[1])*100n+BigInt((match[2]||'').padEnd(2,'0').slice(0,2));}
const money=value=>Number(value)/100;
const status={Pending:0,Processing:2,Shipped:4,Delivered:5,Cancelled:7};
const orders=rows.orders.map(order=>{
  if(!Object.hasOwn(status,order.Status))throw Error('Unknown source order status.');
  const sourceItems=rows.items.filter(i=>i.OrderId===order.Id);
  if(!sourceItems.length||new Set(sourceItems.map(i=>i.ProductId)).size!==sourceItems.length)throw Error('Historical line grouping requires review.');
  const items=sourceItems.map(item=>{
    const productId=catalog.products[item.ProductId],variantId=commerce.variants[item.ProductId];
    if(!productId||!variantId||!Number.isInteger(item.Quantity)||item.Quantity<=0||item.Quantity>1000)throw Error('Missing source mapping or invalid historical quantity.');
    const subtotal=cents(item.UnitPrice)*BigInt(item.Quantity),discount=cents(item.Discount)*BigInt(item.Quantity),total=cents(item.LineTotal);
    if(subtotal-discount!==total)throw Error('Source line totals differ from fixed per-unit discount semantics.');
    const fulfilled=['Shipped','Delivered'].includes(order.Status)?item.Quantity:0,delivered=order.Status==='Delivered'?item.Quantity:0;
    return {productId,variantId,sku:item.Sku,name:item.ProductName,quantity:item.Quantity,fulfilled,delivered,returnRequested:0,returned:0,unitPrice:money(cents(item.UnitPrice)),subtotal:money(subtotal),discount:money(discount),net:money(total),tax:0,total:money(total)};
  });
  const subtotal=cents(order.Subtotal),discount=cents(order.DiscountTotal),shipping=cents(order.ShippingFee),total=cents(order.Total),net=subtotal-discount;
  if(net+shipping!==total||sourceItems.reduce((sum,item)=>sum+cents(item.LineTotal),0n)!==net)throw Error('Source order totals require review.');
  return {reference:'sqlite:'+manifest.sourceSha256+':order:'+order.Id,userId:null,number:order.OrderNumber,status:status[order.Status],paymentState:7,paymentMethod:'NotRecorded',currency:'INR',decimalPlaces:2,
    contact:{fullName:order.CustomerName,email:order.CustomerEmail,phone:order.Phone||null,addressLine1:order.ShippingAddress,addressLine2:null,city:'',countryCode:'',postalCode:''},
    // The legacy application wrote DateTime.UtcNow; retain all seven source fractional digits and the original text in provenance.
    createdAt:order.CreatedAt.replace(' ','T')+'Z',subtotal:money(subtotal),discount:money(discount),net:money(net),tax:0,total:money(total),items,shippingTotal:money(shipping),
    source:{sha256:manifest.sourceSha256,orderId:String(order.Id),customerId:String(order.UserId),status:order.Status,createdAt:order.CreatedAt,shippingAddress:order.ShippingAddress,taxSplitRecorded:false,
      items:sourceItems.map(item=>({itemId:String(item.Id),productId:String(item.ProductId),imageUrl:item.ImageUrl,discountPerUnit:money(cents(item.Discount))}))}};
});
const totals={orders:orders.length,lines:rows.items.length,units:rows.items.reduce((sum,i)=>sum+i.Quantity,0),total:money(rows.orders.reduce((sum,o)=>sum+cents(o.Total),0n)),shipping:money(rows.orders.reduce((sum,o)=>sum+cents(o.ShippingFee),0n))};
if(!process.argv.includes('--apply')){console.log(JSON.stringify({dryRun:true,...totals,unverifiedCustomerLinks:0,missingPaymentMetadata:'NotRecorded',structuredDestination:'NotRecorded',sourceUnchanged:hash(source)===manifest.sourceSha256}));process.exit(0);}
const owner=JSON.parse(readFileSync(join(process.env.APPDATA,'Microsoft/UserSecrets/SRECommerce-LegacyDemo-Development/secrets.json'),'utf8'));
if(owner.TenantId!==catalog.tenantId)throw Error('Dedicated Legacy Demo owner mismatch.');
const tenant=owner.TenantId,identity='http://localhost:5181',tenantApi='http://localhost:5182',orderApi='http://localhost:5189';
await request(orderApi,'/health/ready');
const token=await signIn(identity,owner.OwnerEmail,owner.OwnerPassword,tenant);
const business=await(await request(tenantApi,'/api/v1/tenants/'+tenant,{token})).json();
const access=await(await request(tenantApi,'/api/v1/access/tenants/'+tenant+'/entitlements',{token})).json();
if(!access.modules.some(m=>m.module==='orders'&&m.enabled))await request(tenantApi,'/api/v1/tenants/'+tenant+'/modules/orders',{method:'PUT',token,body:{version:business.version,enabled:true}});
const hex=createHash('sha256').update(manifest.sourceSha256+':'+tenant+':historical-orders:v1').digest('hex').slice(0,32);
const key=hex.replace(/^(.{8})(.{4})(.{4})(.{4})(.{12})$/,'$1-$2-$3-$4-$5');
const body={key,source:'sqlite-orders:'+manifest.sourceSha256.slice(0,16),orders};
const journalPath=join(root,'artifacts/legacy-demo-order-import.json');
const journal=existsSync(journalPath)?JSON.parse(readFileSync(journalPath,'utf8')):{sourceHash:manifest.sourceSha256,tenantId:tenant,request:body};
if(journal.sourceHash!==manifest.sourceSha256||journal.tenantId!==tenant||JSON.stringify(journal.request)!==JSON.stringify(body))throw Error('Historical import journal differs; it was preserved.');
const save=()=>writeFileSync(journalPath,JSON.stringify(journal,null,2));save();
if(!journal.completed||process.argv.includes('--verify-replay')){
  const result=await(await request(orderApi,'/api/v1/tenants/'+tenant+'/order-management/historical-imports',{method:'POST',token,body:journal.request})).json();
  if(result.length!==orders.length||journal.result&&JSON.stringify(journal.result)!==JSON.stringify(result))throw Error('Historical replay identity mismatch.');
  journal.result=result;journal.completed=true;save();
}
for(let index=0;index<orders.length;index++){
  const expected=orders[index],actual=await(await request(orderApi,'/api/v1/tenants/'+tenant+'/order-management/orders/'+journal.result[index],{token})).json();
  if(!actual.historical||actual.number!==expected.number||actual.status!==expected.status||actual.total!==expected.total||actual.shippingTotal!==expected.shippingTotal||actual.itemsTotal!==expected.net||actual.paymentState!==7||actual.paymentMethod!=='NotRecorded'||JSON.stringify(actual.contact)!==JSON.stringify(expected.contact))throw Error('Historical owning-API reconciliation failed.');
  for(const item of expected.items){const line=actual.items.find(i=>i.variantId===item.variantId);if(!line||line.quantity!==item.quantity||line.unitPrice!==item.unitPrice||line.discount!==item.discount||line.total!==item.total)throw Error('Historical line reconciliation failed.');}
}
if(hash(source)!==manifest.sourceSha256)throw Error('Original source changed during backfill; preserved for review.');
journal.verifiedAt=new Date().toISOString();journal.totals=totals;save();
console.log(JSON.stringify({applied:true,...totals,historicalOnly:true,customerLinksCreated:0,sourceUnchanged:true,credentialsPrinted:false}));
