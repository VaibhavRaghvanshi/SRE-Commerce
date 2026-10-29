import assert from 'node:assert/strict';
import {randomBytes,createHash} from 'node:crypto';
import {writeFileSync} from 'node:fs';

const identity=process.env.TENANT_SMOKE_IDENTITY_URL,api=process.env.TENANT_SMOKE_URL,mailpit=process.env.TENANT_SMOKE_MAILPIT_URL;
const adminEmail=process.env.TENANT_SMOKE_ADMIN_EMAIL,adminPassword=process.env.TENANT_SMOKE_ADMIN_PASSWORD;
if(!identity||!api||!mailpit||![identity,api,mailpit].every(value=>['localhost','127.0.0.1'].includes(new URL(value).hostname)))throw new Error('Explicit local Identity/Tenant/Mailpit URLs required. This test creates one sample owner and business.');
if(!adminEmail||!adminPassword)throw new Error('Explicit local development administrator credentials required through environment; values are never printed.');
const checks=[];
async function request(base,path,{method='GET',status=200,body,form,token,cookies,key}={}){
    const headers={};if(token)headers.Authorization='Bearer '+token;if(key)headers['Idempotency-Key']=key;
    if(cookies?.size)headers.Cookie=[...cookies].map(([k,v])=>k+'='+v).join('; ');if(body)headers['Content-Type']='application/json';
    const response=await fetch(base+path,{method,headers,body:body?JSON.stringify(body):form?new URLSearchParams(form):undefined,redirect:'manual',signal:AbortSignal.timeout(20000)});
    if(cookies)for(const cookie of response.headers.getSetCookie()){const pair=cookie.split(';')[0],index=pair.indexOf('=');cookies.set(pair.slice(0,index),pair.slice(index+1));}
    assert.equal(response.status,status,method+' '+path.split('?')[0]+' unexpected status');checks.push({method,path:path.split('?')[0],status:response.status});return response;
}
async function signIn(email,password,tenantId){
    const cookies=new Map(),verifier=randomBytes(48).toString('base64url'),state=randomBytes(16).toString('hex');
    const authorize='/connect/authorize?'+new URLSearchParams({client_id:'srecommerce-local',redirect_uri:'http://127.0.0.1:5099/callback',response_type:'code',scope:'openid profile email offline_access identity_api tenant_api',code_challenge:createHash('sha256').update(verifier).digest('base64url'),code_challenge_method:'S256',state,nonce:randomBytes(16).toString('hex'),...(tenantId?{tenant_id:tenantId}:{})});
    const html=await(await request(identity,'/account/login?returnUrl='+encodeURIComponent(authorize),{cookies})).text();
    const csrf=html.match(/name="__RequestVerificationToken" value="([^"]+)"/)[1];
    await request(identity,'/account/login',{method:'POST',status:302,cookies,form:{email,password,returnUrl:authorize,__RequestVerificationToken:csrf}});
    const authorized=await request(identity,authorize,{status:302,cookies});const callback=new URL(authorized.headers.get('location'));assert.equal(callback.searchParams.get('state'),state);
    return(await(await request(identity,'/connect/token',{method:'POST',cookies,form:{grant_type:'authorization_code',client_id:'srecommerce-local',redirect_uri:'http://127.0.0.1:5099/callback',code:callback.searchParams.get('code'),code_verifier:verifier}})).json()).access_token;
}
await request(api,'/health/ready');
const email='tenant-smoke-'+randomBytes(8).toString('hex')+'@example.test',password='Aa1!'+randomBytes(24).toString('base64');
await request(identity,'/api/v1/accounts/register',{method:'POST',status:202,body:{email,password,fullName:'Tenant smoke owner',kind:1}});
let confirmation;
for(let attempt=0;attempt<60&&!confirmation;attempt++){
    const list=await(await fetch(mailpit+'/api/v1/messages',{signal:AbortSignal.timeout(10000)})).json();const message=list.messages.find(m=>m.To?.some(to=>to.Address===email));
    if(message){const content=await(await fetch(mailpit+'/api/v1/message/'+message.ID,{signal:AbortSignal.timeout(10000)})).json();confirmation=JSON.parse(content.Text.trim()).code;}
    if(!confirmation)await new Promise(resolve=>setTimeout(resolve,500));
}
assert.ok(confirmation,'Confirmation email not captured');await request(identity,'/api/v1/accounts/confirm-email',{method:'POST',status:204,body:{email,code:confirmation}});
let owner=await signIn(email,password);const onboardingOwner=owner;const key=randomBytes(16).toString('hex').replace(/^(.{8})(.{4})(.{4})(.{4})(.{12})$/,'$1-$2-$3-$4-$5');
const registration={name:'Tenant container smoke',slug:'tenant-smoke-'+randomBytes(8).toString('hex'),businessTypeCode:'grocery'};
let tenant=await(await request(api,'/api/v1/tenants',{method:'POST',status:201,token:owner,key,body:registration})).json();
assert.equal((await(await request(api,'/api/v1/tenants',{method:'POST',status:201,token:owner,key,body:registration})).json()).id,tenant.id);
const route='/api/v1/tenants/'+tenant.id,platform='/api/v1/platform/tenants/'+tenant.id;
tenant=await(await request(api,route+'/settings',{method:'PUT',token:owner,body:{version:tenant.version,currency:'INR',culture:'en-IN',timeZone:'Asia/Kolkata',displayName:'Tenant smoke store',logoUrl:null}})).json();
tenant=await(await request(api,route+'/submit',{method:'POST',token:owner,body:{version:tenant.version}})).json();
await request(api,platform+'/activate',{method:'POST',status:403,token:owner,body:{version:tenant.version}});
const administrator=await signIn(adminEmail,adminPassword);
tenant=await(await request(api,platform+'/activate',{method:'POST',token:administrator,body:{version:tenant.version}})).json();
owner=await signIn(email,password,tenant.id);
assert.equal((await(await request(api,route+'/entitlements',{token:owner})).json()).active,true);
await request(api,route+'/modules/manufacturing',{method:'PUT',status:409,token:owner,body:{version:tenant.version,enabled:true}});
tenant=await(await request(api,route+'/locations',{method:'POST',token:owner,body:{version:tenant.version,name:'Smoke main location',address:'Development sample address',countryCode:'IN'}})).json();
await request(api,route+'/locations',{method:'POST',status:409,token:owner,body:{version:tenant.version,name:'Over quota',address:'Development sample address',countryCode:'IN'}});
tenant=await(await request(api,platform+'/suspend',{method:'POST',token:administrator,body:{version:tenant.version,reason:'Development smoke lifecycle'}})).json();
assert.equal((await(await request(api,route+'/entitlements',{token:owner})).json()).active,false);
tenant=await(await request(api,platform+'/activate',{method:'POST',token:administrator,body:{version:tenant.version}})).json();
assert.equal((await(await request(api,route+'/entitlements',{token:owner})).json()).active,true);
assert.ok((await(await request(api,route+'/audit',{token:owner})).json()).some(e=>e.action==='tenant.activated'));
await request(identity,'/api/v1/logout',{method:'POST',status:204,token:owner});await request(api,route+'/settings',{status:401,token:owner});await request(identity,'/api/v1/logout',{method:'POST',status:204,token:onboardingOwner});await request(identity,'/api/v1/logout',{method:'POST',status:204,token:administrator});
const report={checksPassed:checks.length,tenantId:tenant.id,pkceVerified:true,ownerProvisioningVerified:true,quotaVerified:true,suspensionVerified:true,revocationVerified:true,credentialsLogged:false,checks};
if(process.env.TENANT_SMOKE_REPORT)writeFileSync(process.env.TENANT_SMOKE_REPORT,JSON.stringify(report,null,2));console.log(JSON.stringify({...report,checks:undefined}));
