import {DatabaseSync} from 'node:sqlite';
import {createHash} from 'node:crypto';
import {readFileSync,writeFileSync,existsSync} from 'node:fs';
import {resolve,join,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
import {request,signIn} from './lib/local-commerce.mjs';

// Source databases are read-only. Each service owns its SQL transaction and import ledger.
const root=resolve(dirname(fileURLToPath(import.meta.url)),'..');
const source=join(root,'SRECommerce.Legacy.API/SRECommerce.Api/App_Data/srecommerce.db');
const baseline=join(root,'artifacts/baseline-20261028-232640');
const manifest=JSON.parse(readFileSync(join(baseline,'sqlite-manifest.json'),'utf8'));
const hashFile=path=>createHash('sha256').update(readFileSync(path)).digest('hex');
if(hashFile(source)!==manifest.sourceSha256)throw Error('Source hash differs from approved baseline. No import attempted.');
const catalogMap=JSON.parse(readFileSync(join(root,'artifacts/legacy-demo-catalog-mapping.json'),'utf8'));
const inventoryMap=JSON.parse(readFileSync(join(root,'artifacts/legacy-demo-inventory-mapping.json'),'utf8'));
if(catalogMap.sourceHash!==manifest.sourceSha256||inventoryMap.sourceHash!==manifest.sourceSha256||catalogMap.tenantId!==inventoryMap.tenantId)throw Error('Migration mapping/source mismatch.');
const db=new DatabaseSync(join(baseline,'srecommerce.backup.db'),{readOnly:true});
const productSql='SELECT Id,Price,Discount,StockQty,Sku FROM Products ORDER BY Id';
const products=db.prepare(productSql).all();
if(db.prepare('PRAGMA integrity_check').get().integrity_check!=='ok')throw Error('Backup integrity check failed.');db.close();
// SQLite's backup API can change file header bytes. Compare logical rows, not source/backup file hashes.
const current=new DatabaseSync(source,{readOnly:true});const currentRows=current.prepare(productSql).all();current.close();
if(JSON.stringify(products)!==JSON.stringify(currentRows))throw Error('Backup product rows differ from the approved source.');
for(const p of products){p.Price=Number(p.Price);p.Discount=Number(p.Discount??0);if(!catalogMap.products[p.Id]||!Number.isFinite(p.Price)||!Number.isFinite(p.Discount)||p.Price<0||p.Discount<0||p.Discount>p.Price||!Number.isInteger(p.StockQty)||p.StockQty<0)throw Error('Invalid source row requires review; originals preserved.');}
const totals={products:products.length,quantity:products.reduce((sum,p)=>sum+p.StockQty,0),mrp:products.reduce((sum,p)=>sum+p.Price,0),selling:products.reduce((sum,p)=>sum+p.Price-p.Discount,0)};
if(products.length!==1000)throw Error('Unexpected source count.');
if(!process.argv.includes('--apply')){console.log(JSON.stringify({dryRun:true,...totals,discountMeaning:'fixed per unit',unknownCost:null,sourceUnchanged:true}));process.exit(0);}
const owner=JSON.parse(readFileSync(join(process.env.APPDATA,'Microsoft/UserSecrets/SRECommerce-LegacyDemo-Development/secrets.json'),'utf8'));
if(owner.TenantId!==catalogMap.tenantId)throw Error('Owner tenant mismatch.');
const tenant=owner.TenantId,bases={catalog:'http://localhost:5185',inventory:'http://localhost:5186',pricing:'http://localhost:5187'};
let token=await signIn('http://localhost:5181',owner.OwnerEmail,owner.OwnerPassword,tenant);
const journalPath=join(root,'artifacts/legacy-demo-commerce-import.json');
const journal=existsSync(journalPath)?JSON.parse(readFileSync(journalPath,'utf8')):{sourceHash:manifest.sourceSha256,tenantId:tenant,batches:{},variants:{},totals};
if(journal.sourceHash!==manifest.sourceSha256||journal.tenantId!==tenant)throw Error('Different import journal preserved.');
const save=()=>writeFileSync(journalPath,JSON.stringify(journal,null,2));
const key=label=>{const hex=createHash('sha256').update(manifest.sourceSha256+':'+tenant+':'+label).digest('hex').slice(0,32);return hex.replace(/^(.{8})(.{4})(.{4})(.{4})(.{12})$/,'$1-$2-$3-$4-$5');};
async function api(service,path,body,status=200){return(await request(bases[service],'/api/v1/tenants/'+tenant+'/'+service+path,{token,method:body===undefined?'GET':'POST',body,status})).json();}
async function batch(service,label,path,make){
  let record=journal.batches[label];
  if(!record){record={request:await make()};journal.batches[label]=record;save();}
  if(!record.completed||process.argv.includes('--verify-replay')){
    const result=await api(service,path,record.request);if(result.length!==record.request.products?.length&&result.length!==record.request.rows?.length&&result.length!==record.request.prices?.length)throw Error('Unexpected batch result count.');
    if(record.result&&JSON.stringify(record.result)!==JSON.stringify(result))throw Error('Replay returned different identities.');
    record.result=result;record.completed=true;save();
  }
}
// Read draft versions once; journal the exact publication request before sending it.
const drafts=new Map();for(let page=1;;page++){
  const result=await api('catalog','/products?page='+page+'&pageSize=100');for(const product of result.items)drafts.set(product.id,product);if(page*100>=result.total)break;
}
for(let start=0;start<products.length;start+=100){
  const chunk=products.slice(start,start+100),label='publish:'+start;
  await batch('catalog',label,'/publish-batch',()=>({key:key(label),source:'legacy-publish:'+manifest.sourceSha256.slice(0,16)+':'+start,products:chunk.map(p=>{const product=drafts.get(catalogMap.products[p.Id]);if(!product||product.status===2)throw Error('Mapped draft missing or archived.');return {productId:product.id,version:product.version};})}));
  for(const p of chunk){const product=drafts.get(catalogMap.products[p.Id]);if(product.variants.length!==1||product.variants[0].sku!==p.Sku.toUpperCase())throw Error('Source/catalog SKU mismatch.');journal.variants[p.Id]=product.variants[0].id;}save();
  console.log('Published source products: '+(start+chunk.length)+'/1000');
}
let lookups=await api('pricing','/lookups');
if(!lookups.settings){await request(bases.pricing,'/api/v1/tenants/'+tenant+'/pricing/settings',{method:'PUT',token,body:{currency:lookups.currency,decimalPlaces:2}});lookups=await api('pricing','/lookups');}
if(lookups.settings.currency!=='INR'||lookups.settings.decimalPlaces!==2)throw Error('Legacy INR precision differs from configured tenant; no prices imported.');
let tax=lookups.taxes.find(t=>t.code==='LEGACY_DEMO_NO_TAX_METADATA');
if(!tax)tax=await api('pricing','/tax-policies',{code:'LEGACY_DEMO_NO_TAX_METADATA',name:'Development only: source has no tax metadata',rate:0,inclusive:false},201);
if(tax.rate!==0||tax.inclusive||!tax.active)throw Error('Dedicated legacy tax adapter changed; preserved for review.');
let list=lookups.lists.find(l=>l.code==='LEGACY_RETAIL');
if(!list)list=await api('pricing','/price-lists',{code:'LEGACY_RETAIL',name:'Legacy Demo retail (source prices)',currency:'INR',audience:0,customerGroupId:null,priority:0,startsAt:new Date(Date.now()-86400000).toISOString(),endsAt:new Date(Date.now()+3650*86400000).toISOString()},201);
if(!list.active||list.audience!==0||list.currency!=='INR')throw Error('Dedicated legacy retail list changed.');
journal.priceListId=list.id;journal.taxPolicyId=tax.id;save();
for(let start=0;start<products.length;start+=100){
  const chunk=products.slice(start,start+100),priceLabel='price:'+start,stockLabel='stock:'+start;
  await batch('pricing',priceLabel,'/imports',()=>({key:key(priceLabel),source:'legacy-price:'+manifest.sourceSha256.slice(0,16)+':'+start,prices:chunk.map(p=>({priceListId:list.id,productId:catalogMap.products[p.Id],variantId:journal.variants[p.Id],taxPolicyId:tax.id,minimumQuantity:1,mrp:p.Price,sellingPrice:Math.round((p.Price-p.Discount)*10000)/10000,costPrice:null,active:true}))}));
  await batch('inventory',stockLabel,'/opening-imports',()=>({key:key(stockLabel),source:'legacy-stock:'+manifest.sourceSha256.slice(0,16)+':'+start,warehouseId:inventoryMap.warehouseId,lot:'LEGACY-'+manifest.sourceSha256.slice(0,12).toUpperCase(),rows:chunk.map(p=>({productId:catalogMap.products[p.Id],variantId:journal.variants[p.Id],quantity:p.StockQty}))}));
  console.log('Source prices and opening stock imported: '+(start+chunk.length)+'/1000');
}
// Reconcile every imported tier and opening lot through the owning APIs, not cross-service SQL.
const prices=[];for(let page=1;;page++){const result=await api('pricing','/prices?page='+page+'&list='+list.id);prices.push(...result.items);if(prices.length>=result.total)break;}
const balances=await api('inventory','/balances?warehouse='+inventoryMap.warehouseId),inventory=await api('inventory','/lookups');
for(const p of products){const variant=journal.variants[p.Id],price=prices.find(row=>row.variantId===variant&&row.minimumQuantity===1),stock=balances.filter(row=>row.variantId===variant&&row.lot==='LEGACY-'+manifest.sourceSha256.slice(0,12).toUpperCase());
  if(!price||price.productId!==catalogMap.products[p.Id]||price.mrp!==p.Price||Math.abs(price.sellingPrice-(p.Price-p.Discount))>0.00001||price.costPrice!==null||!inventory.items.some(item=>item.id===variant)||stock.reduce((sum,row)=>sum+row.onHand,0)!==p.StockQty)throw Error('Owning-service reconciliation failed for source row '+p.Id);
}
if(hashFile(source)!==manifest.sourceSha256)throw Error('Source hash changed unexpectedly.');
journal.verifiedAt=new Date().toISOString();journal.sourceUnchanged=true;save();inventoryMap.stockBackfill='verified';inventoryMap.sourceQuantity=totals.quantity;writeFileSync(join(root,'artifacts/legacy-demo-inventory-mapping.json'),JSON.stringify(inventoryMap,null,2));
console.log(JSON.stringify({verified:true,...totals,unknownCostPreserved:true,taxAdapter:'explicit development zero; no source tax metadata',sourceUnchanged:true,credentialsPrinted:false}));
