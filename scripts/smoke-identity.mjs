import { randomBytes, createHash } from 'node:crypto';
import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';

const api=process.env.IDENTITY_SMOKE_URL;
const mailpit=process.env.IDENTITY_MAILPIT_URL;
if(!api || !mailpit || ![api,mailpit].every(value=>['localhost','127.0.0.1'].includes(new URL(value).hostname))) throw new Error('Explicit local Identity and dedicated Mailpit URLs required. This test creates a sample account.');
const cookies=new Map(); const checks=[];
async function request(path,{method='GET',body,form,token,status=200}={}){
    const headers={};if(cookies.size)headers.Cookie=[...cookies].map(([k,v])=>k+'='+v).join('; ');
    if(token)headers.Authorization='Bearer '+token;
    if(body)headers['Content-Type']='application/json';
    const response=await fetch(api+path,{method,headers,body:body?JSON.stringify(body):form?new URLSearchParams(form):undefined,redirect:'manual',signal:AbortSignal.timeout(15000)});
    for(const cookie of response.headers.getSetCookie()){const value=cookie.split(';')[0],index=value.indexOf('=');cookies.set(value.slice(0,index),value.slice(index+1));}
    assert.equal(response.status,status,method+' '+path.split('?')[0]+' unexpected status');
    checks.push({method,path:path.split('?')[0],status:response.status});return response;
}
await request('/health/ready');
const discovery=await(await request('/.well-known/openid-configuration')).json();
assert.ok(discovery.code_challenge_methods_supported.includes('S256'));
const email='identity-smoke-'+randomBytes(8).toString('hex')+'@example.test';
const password='Aa1!'+randomBytes(24).toString('base64');
await request('/api/v1/accounts/register',{method:'POST',status:202,body:{email,password,fullName:'Identity smoke sample'}});
let code;
for(let attempt=0;attempt<30 && !code;attempt++){
    const list=await(await fetch(mailpit+'/api/v1/messages')).json();
    const message=list.messages.find(m=>m.To?.some(to=>to.Address===email));
    if(message){const content=await(await fetch(mailpit+'/api/v1/message/'+message.ID)).json();code=JSON.parse(content.Text.trim()).code;}
    if(!code)await new Promise(resolve=>setTimeout(resolve,500));
}
assert.ok(code,'Verification message was not captured in dedicated local Mailpit');
await request('/api/v1/accounts/confirm-email',{method:'POST',status:204,body:{email,code}});
const verifier=randomBytes(48).toString('base64url'),state=randomBytes(16).toString('hex');
const authorize='/connect/authorize?'+new URLSearchParams({client_id:'srecommerce-local',redirect_uri:'http://127.0.0.1:5099/callback',response_type:'code',scope:'openid profile email offline_access identity_api',code_challenge:createHash('sha256').update(verifier).digest('base64url'),code_challenge_method:'S256',state,nonce:randomBytes(16).toString('hex')});
const html=await(await request('/account/login?returnUrl='+encodeURIComponent(authorize))).text();
const csrf=html.match(/name="__RequestVerificationToken" value="([^"]+)"/)[1];
await request('/account/login',{method:'POST',status:302,form:{email,password,returnUrl:authorize,__RequestVerificationToken:csrf}});
const authorized=await request(authorize,{status:302});
const callback=new URL(authorized.headers.get('location'));assert.equal(callback.searchParams.get('state'),state);
const exchanged=await request('/connect/token',{method:'POST',form:{grant_type:'authorization_code',client_id:'srecommerce-local',redirect_uri:'http://127.0.0.1:5099/callback',code:callback.searchParams.get('code'),code_verifier:verifier}});
const token=await exchanged.json();
assert.ok(token.id_token && token.refresh_token);
const me=await(await request('/api/v1/me',{token:token.access_token})).json();assert.equal(me.email,email);
await request('/api/v1/platform/audit',{token:token.access_token,status:403});
const refresh=await(await request('/connect/token',{method:'POST',form:{grant_type:'refresh_token',client_id:'srecommerce-local',refresh_token:token.refresh_token}})).json();
assert.notEqual(refresh.refresh_token,token.refresh_token);
await request('/api/v1/logout',{method:'POST',status:204,token:refresh.access_token});
await request('/api/v1/me',{status:401,token:refresh.access_token});
const report={checksPassed:checks.length,mailpitDeliveryVerified:true,pkceVerified:true,refreshRotationVerified:true,logoutRevocationVerified:true,credentialsLogged:false,checks};
if(process.env.IDENTITY_SMOKE_REPORT)writeFileSync(process.env.IDENTITY_SMOKE_REPORT,JSON.stringify(report,null,2));
console.log(JSON.stringify({...report,checks:undefined}));
