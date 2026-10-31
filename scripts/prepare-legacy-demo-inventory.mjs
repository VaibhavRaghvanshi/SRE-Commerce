import {readFileSync,writeFileSync} from 'node:fs';
import {join,resolve} from 'node:path';
import {createHash} from 'node:crypto';
import {request,signIn} from './lib/local-commerce.mjs';
if(!process.argv.includes('--apply'))throw new Error('Explicit --apply required for approved local Legacy Demo inventory setup.');
const root=resolve('.'),identity='http://localhost:5181',tenantApi='http://localhost:5182',inventory='http://localhost:5186';
const config=JSON.parse(readFileSync(join(process.env.APPDATA,'Microsoft/UserSecrets/SRECommerce-LegacyDemo-Development/secrets.json'),'utf8'));
const source=join(root,'SRECommerce.Legacy.API/SRECommerce.Api/App_Data/srecommerce.db'),mapping=JSON.parse(readFileSync(join(root,'artifacts/legacy-demo-catalog-mapping.json'),'utf8'));
const sourceHash=()=>createHash('sha256').update(readFileSync(source)).digest('hex');if(sourceHash()!==mapping.sourceHash||mapping.tenantId!==config.TenantId)throw new Error('Source/mapping mismatch; no data changed.');
let token=await signIn(identity,config.OwnerEmail,config.OwnerPassword,config.TenantId),tenant=await(await request(tenantApi,'/api/v1/tenants/'+config.TenantId,{token})).json();
const entitlements=await(await request(tenantApi,'/api/v1/tenants/'+tenant.id+'/entitlements',{token})).json();
if(!entitlements.modules.some(m=>m.module==='inventory'&&m.enabled)){
  // Development plan only: no billing provider, payment or real subscription charge.
  const bootstrap=JSON.parse(readFileSync(join(process.env.APPDATA,'Microsoft/UserSecrets/SRECommerce-Identity-Development/secrets.json'),'utf8'));
  const admin=await signIn(identity,bootstrap['Bootstrap:Email'],bootstrap['Bootstrap:Password']);
  tenant=await(await request(tenantApi,'/api/v1/platform/tenants/'+tenant.id+'/subscription',{method:'PUT',token:admin,body:{version:tenant.version,planCode:'growth',startsAt:new Date(Date.now()-60000).toISOString(),endsAt:new Date(Date.now()+365*86400000).toISOString(),status:1}})).json();
  tenant=await(await request(tenantApi,'/api/v1/tenants/'+tenant.id+'/modules/inventory',{method:'PUT',token,body:{version:tenant.version,enabled:true}})).json();
}
const base='/api/v1/tenants/'+tenant.id+'/inventory';let lookups=await(await request(inventory,base+'/lookups',{token})).json();
let branch=lookups.branches.find(b=>b.code==='LEGACY-DEMO');if(!branch)branch=await(await request(inventory,base+'/branches',{method:'POST',status:201,token,body:{code:'LEGACY-DEMO',name:'Legacy Demo branch'}})).json();
let warehouse=lookups.warehouses.find(w=>w.code==='LEGACY-DEMO');if(!warehouse)warehouse=await(await request(inventory,base+'/warehouses',{method:'POST',status:201,token,body:{branchId:branch.id,code:'LEGACY-DEMO',name:'Legacy Demo warehouse'}})).json();
if(sourceHash()!==mapping.sourceHash)throw new Error('Source hash changed unexpectedly.');
writeFileSync(join(root,'artifacts/legacy-demo-inventory-mapping.json'),JSON.stringify({tenantId:tenant.id,branchId:branch.id,warehouseId:warehouse.id,sourceHash:mapping.sourceHash,stockBackfill:'pending',credentialsPrinted:false},null,2));
console.log(JSON.stringify({tenant:'Legacy Demo',inventoryModule:true,warehouse:true,sourceUnchanged:true,stockBackfill:'pending',credentialsPrinted:false}));
