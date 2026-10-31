import {readFileSync} from 'node:fs';
import {join,resolve} from 'node:path';
import {createHash} from 'node:crypto';
import {request,signIn} from './lib/local-commerce.mjs';
if(!process.argv.includes('--apply'))throw new Error('Explicit --apply required for local Legacy Demo cart setup.');
const root=resolve('.'),identity='http://localhost:5181',tenantApi='http://localhost:5182';
const c=JSON.parse(readFileSync(join(process.env.APPDATA,'Microsoft/UserSecrets/SRECommerce-LegacyDemo-Development/secrets.json'),'utf8'));
const mapping=JSON.parse(readFileSync(join(root,'artifacts/legacy-demo-catalog-mapping.json'),'utf8'));const source=join(root,'SRECommerce.Legacy.API/SRECommerce.Api/App_Data/srecommerce.db');
const hash=()=>createHash('sha256').update(readFileSync(source)).digest('hex');if(hash()!==mapping.sourceHash||mapping.tenantId!==c.TenantId)throw new Error('Source/mapping mismatch; no data changed.');
const token=await signIn(identity,c.OwnerEmail,c.OwnerPassword,c.TenantId);const tenant=await(await request(tenantApi,'/api/v1/tenants/'+c.TenantId,{token})).json();
const state=await(await request(tenantApi,'/api/v1/tenants/'+tenant.id+'/entitlements',{token})).json();
if(!state.modules.some(m=>m.module==='cart'&&m.enabled)){
  await request(tenantApi,'/api/v1/tenants/'+tenant.id+'/modules/cart',{method:'PUT',token,body:{version:tenant.version,enabled:true}});
}
if(hash()!==mapping.sourceHash)throw new Error('Source hash changed unexpectedly.');
console.log(JSON.stringify({tenant:'Legacy Demo',cartModule:true,checkout:'Cart readiness only; Order and payment pending',sourceUnchanged:true,credentialsPrinted:false}));
