import {DatabaseSync} from 'node:sqlite';
import {createHash,randomBytes} from 'node:crypto';
import {readFileSync,writeFileSync,existsSync,mkdirSync} from 'node:fs';
import {resolve,join} from 'node:path';

// Approved, non-destructive Catalog slice of the Legacy Demo backfill. Price/stock/order
// authority is migrated by the later owning services; originals and their values stay preserved.
if(!process.argv.includes('--apply'))throw new Error('Explicit --apply required for the approved Legacy Demo Catalog backfill.');
const root=resolve('.'),identity='http://localhost:5181',tenantApi='http://localhost:5182',catalog='http://localhost:5185',mailpit='http://localhost:8025';
const baseline=join(root,'artifacts/baseline-20261028-232640'),source=join(root,'SRECommerce.Legacy.API/SRECommerce.Api/App_Data/srecommerce.db');
const manifest=JSON.parse(readFileSync(join(baseline,'sqlite-manifest.json'),'utf8'));
const sourceHash=()=>createHash('sha256').update(readFileSync(source)).digest('hex');
if(sourceHash()!==manifest.sourceSha256)throw new Error('Current source hash differs from the verified backup; originals were not changed.');
const secretDir=join(process.env.APPDATA,'Microsoft/UserSecrets/SRECommerce-LegacyDemo-Development');mkdirSync(secretDir,{recursive:true});
const secretFile=join(secretDir,'secrets.json');const config=existsSync(secretFile)?JSON.parse(readFileSync(secretFile,'utf8')):{};
config.OwnerEmail??='legacy-demo-owner-'+randomBytes(6).toString('hex')+'@srecommerce.local';config.OwnerPassword??='Aa1!'+randomBytes(32).toString('hex');
const saveConfig=()=>writeFileSync(secretFile,JSON.stringify(config,null,2));saveConfig();
const output=join(root,'artifacts/legacy-demo-catalog-mapping.json');const mapping=existsSync(output)?JSON.parse(readFileSync(output,'utf8')):{sourceHash:manifest.sourceSha256,categories:{},products:{},pendingPricing:[],pendingInventory:[]};
if(mapping.sourceHash!==manifest.sourceSha256)throw new Error('A different backfill source is already recorded; it was preserved.');
const saveMapping=()=>writeFileSync(output,JSON.stringify(mapping,null,2));
async function request(base,path,{method='GET',body,token,status=200,key,cookies,form}={}){
  const headers={};if(body)headers['Content-Type']='application/json';if(token)headers.Authorization='Bearer '+token;if(key)headers['Idempotency-Key']=key;if(cookies?.size)headers.Cookie=[...cookies].map(([k,v])=>k+'='+v).join('; ');
  const response=await fetch(base+path,{method,headers,body:body?JSON.stringify(body):form?new URLSearchParams(form):undefined,redirect:'manual',signal:AbortSignal.timeout(45000)});
  if(cookies)for(const value of response.headers.getSetCookie()){const pair=value.split(';')[0],at=pair.indexOf('=');cookies.set(pair.slice(0,at),pair.slice(at+1));}
  if(response.status!==status)throw new Error(method+' '+path.split('?')[0]+' returned '+response.status+'; no credentials printed.');return response;
}
async function signIn(email,password,tenantId){
  const cookies=new Map(),verifier=randomBytes(48).toString('base64url'),state=randomBytes(16).toString('hex');
  const authorization='/connect/authorize?'+new URLSearchParams({client_id:'srecommerce-local',redirect_uri:'http://127.0.0.1:5099/callback',response_type:'code',scope:'openid profile email offline_access identity_api tenant_api commerce_api',tenant_id:tenantId||'',state,nonce:randomBytes(16).toString('hex'),code_challenge:createHash('sha256').update(verifier).digest('base64url'),code_challenge_method:'S256'});
  const html=await(await request(identity,'/account/login?returnUrl='+encodeURIComponent(authorization),{cookies})).text();const csrf=html.match(/name="__RequestVerificationToken" value="([^"]+)"/)?.[1];if(!csrf)throw new Error('Local login form unavailable');
  await request(identity,'/account/login',{method:'POST',status:302,cookies,form:{email,password,returnUrl:authorization,__RequestVerificationToken:csrf.replaceAll('&amp;','&')}});
  const response=await request(identity,authorization,{status:302,cookies});const callback=new URL(response.headers.get('location'));if(callback.searchParams.get('state')!==state||!callback.searchParams.get('code'))throw new Error('Authorization failed; no credentials printed.');
  return(await(await request(identity,'/connect/token',{method:'POST',form:{grant_type:'authorization_code',client_id:'srecommerce-local',redirect_uri:'http://127.0.0.1:5099/callback',code:callback.searchParams.get('code'),code_verifier:verifier}})).json()).access_token;
}
if(!config.Confirmed){
  await request(identity,'/api/v1/accounts/register',{method:'POST',status:202,body:{email:config.OwnerEmail,password:config.OwnerPassword,fullName:'Legacy Demo Owner (development)',kind:1}});
  let code;for(let attempt=0;attempt<30&&!code;attempt++){
    const list=await(await request(mailpit,'/api/v1/messages')).json();const message=list.messages.find(m=>m.To?.some(to=>to.Address===config.OwnerEmail));
    if(message){const content=await(await request(mailpit,'/api/v1/message/'+message.ID)).json();code=JSON.parse(content.Text.trim()).code;}
    if(!code)await new Promise(r=>setTimeout(r,400));
  }
  if(!code)throw new Error('Confirmation email is pending in local Mailpit; owner credentials and source data were preserved.');
  await request(identity,'/api/v1/accounts/confirm-email',{method:'POST',status:204,body:{email:config.OwnerEmail,code}});config.Confirmed=true;saveConfig();
}
let owner=await signIn(config.OwnerEmail,config.OwnerPassword);
let tenant;
if(!config.TenantId){
  tenant=await(await request(tenantApi,'/api/v1/tenants',{method:'POST',status:201,token:owner,key:'d98965f1-9eca-47d0-a46f-078b5505b685',body:{name:'Legacy Demo',slug:'legacy-demo',businessTypeCode:'general-store'}})).json();config.TenantId=tenant.id;saveConfig();
}else tenant=await(await request(tenantApi,'/api/v1/tenants/'+config.TenantId,{token:owner})).json();
if(tenant.status===0)tenant=await(await request(tenantApi,'/api/v1/tenants/'+tenant.id+'/submit',{method:'POST',token:owner,body:{version:tenant.version}})).json();
if(tenant.status===1){
  const bootstrap=JSON.parse(readFileSync(join(process.env.APPDATA,'Microsoft/UserSecrets/SRECommerce-Identity-Development/secrets.json'),'utf8'));
  const admin=await signIn(bootstrap['Bootstrap:Email'],bootstrap['Bootstrap:Password']);tenant=await(await request(tenantApi,'/api/v1/platform/tenants/'+tenant.id+'/activate',{method:'POST',token:admin,body:{version:tenant.version}})).json();
}
if(tenant.status!==2)throw new Error('Legacy Demo tenant is not active; lifecycle was preserved.');
owner=await signIn(config.OwnerEmail,config.OwnerPassword,tenant.id);mapping.tenantId=tenant.id;
const db=new DatabaseSync(join(baseline,'srecommerce.backup.db'),{readOnly:true});const categories=db.prepare('SELECT * FROM Categories ORDER BY Id').all(),products=db.prepare('SELECT * FROM Products ORDER BY Id').all();db.close();
if(products.length!==1000)throw new Error('Unexpected source product count; originals were preserved.');
const base='/api/v1/tenants/'+tenant.id+'/catalog';
let lookups=await(await request(catalog,base+'/lookups',{token:owner})).json();
const orderedCategories=[];const pendingCategories=[...categories];const categoryIds=new Set(categories.map(c=>c.Id));
while(pendingCategories.length){const next=pendingCategories.findIndex(c=>!c.ParentId||orderedCategories.some(p=>p.Id===c.ParentId));if(next<0)throw new Error('Source category hierarchy is cyclic or incomplete.');const [category]=pendingCategories.splice(next,1);if(category.ParentId&&!categoryIds.has(category.ParentId))throw new Error('Unknown source parent category');orderedCategories.push(category);}
for(const category of orderedCategories){
  if(mapping.categories[category.Id])continue;
  const slug=/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(category.Slug)?category.Slug:'legacy-category-'+category.Id;
  let value=lookups.categories.find(c=>c.slug===slug);
  if(!value)value=await(await request(catalog,base+'/categories',{method:'POST',status:201,token:owner,body:{name:category.Name,slug,parentId:category.ParentId?mapping.categories[category.ParentId]:null}})).json();
  mapping.categories[category.Id]=value.id;saveMapping();
}
for(let start=0;start<products.length;start+=100){
  const chunk=products.slice(start,start+100);if(chunk.every(p=>mapping.products[p.Id]))continue;
  const hash=createHash('sha256').update(manifest.sourceSha256+':catalog:'+start).digest('hex').slice(0,32);const key=hash.replace(/^(.{8})(.{4})(.{4})(.{4})(.{12})$/,'$1-$2-$3-$4-$5');
  const drafts=chunk.map(p=>({name:p.Name,slug:/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(p.Slug)?p.Slug:'legacy-product-'+p.Id,categoryId:mapping.categories[p.CategoryId],brandId:null,kind:0,description:'',seoTitle:p.Name.slice(0,160),seoDescription:'',visible:true,b2BOnly:false,attributes:{},specifications:{},variants:[{id:null,sku:p.Sku,options:{},barcode:null,active:true}],media:p.ImageUrl?[{id:null,url:p.ImageUrl,alt:p.Name.slice(0,200),order:1}]:[],bundleItems:[],relationships:[],version:null}));
  const ids=await(await request(catalog,base+'/import',{method:'POST',token:owner,body:{key,source:'legacy-sqlite:'+manifest.sourceSha256.slice(0,16)+':'+start,products:drafts}})).json();
  chunk.forEach((p,i)=>{mapping.products[p.Id]=ids[i];mapping.pendingPricing.push({legacyId:p.Id,productId:ids[i],price:p.Price,discount:p.Discount});mapping.pendingInventory.push({legacyId:p.Id,productId:ids[i],stock:p.StockQty,sku:p.Sku});});saveMapping();
  console.log('Catalog drafts imported: '+Object.keys(mapping.products).length+'/'+products.length);
}
if(sourceHash()!==manifest.sourceSha256)throw new Error('Source hash changed unexpectedly; migration evidence preserved.');
console.log(JSON.stringify({tenant:'Legacy Demo',products:Object.keys(mapping.products).length,categories:Object.keys(mapping.categories).length,sourceUnchanged:true,published:false,pricingAndInventory:'pending owning services',credentialsPrinted:false}));
