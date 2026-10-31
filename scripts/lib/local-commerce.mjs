import {createHash,randomBytes} from 'node:crypto';
// Test/development protocol helper. Never log passwords, bearer tokens or OAuth callback URLs.
export async function request(base,path,{method='GET',body,token,status=200,cookies,form}={}){
  if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Only an explicit loopback development API is supported.');
  const headers={};if(body!==undefined)headers['Content-Type']='application/json';if(token)headers.Authorization='Bearer '+token;if(cookies?.size)headers.Cookie=[...cookies].map(([k,v])=>k+'='+v).join('; ');
  const response=await fetch(base+path,{method,headers,body:body!==undefined?JSON.stringify(body):form?new URLSearchParams(form):undefined,redirect:'manual',signal:AbortSignal.timeout(45000)});
  if(cookies)for(const value of response.headers.getSetCookie()){const pair=value.split(';')[0],at=pair.indexOf('=');cookies.set(pair.slice(0,at),pair.slice(at+1));}
  if(response.status!==status)throw new Error(method+' '+path.split('?')[0]+' returned '+response.status+'; private values were not printed.');return response;
}
export async function signIn(identity,email,password,tenantId){
  const cookies=new Map(),verifier=randomBytes(48).toString('base64url'),state=randomBytes(16).toString('hex');
  const authorization='/connect/authorize?'+new URLSearchParams({client_id:'srecommerce-local',redirect_uri:'http://127.0.0.1:5099/callback',response_type:'code',scope:'openid profile email offline_access identity_api tenant_api commerce_api',tenant_id:tenantId||'',state,nonce:randomBytes(16).toString('hex'),code_challenge:createHash('sha256').update(verifier).digest('base64url'),code_challenge_method:'S256'});
  const html=await(await request(identity,'/account/login?returnUrl='+encodeURIComponent(authorization),{cookies})).text();const csrf=html.match(/name="__RequestVerificationToken" value="([^"]+)"/)?.[1];if(!csrf)throw new Error('Local login form unavailable.');
  await request(identity,'/account/login',{method:'POST',status:302,cookies,form:{email,password,returnUrl:authorization,__RequestVerificationToken:csrf.replaceAll('&amp;','&')}});
  const response=await request(identity,authorization,{status:302,cookies});const callback=new URL(response.headers.get('location'));if(callback.searchParams.get('state')!==state||!callback.searchParams.get('code'))throw new Error('Authorization failed; private values were not printed.');
  return(await(await request(identity,'/connect/token',{method:'POST',form:{grant_type:'authorization_code',client_id:'srecommerce-local',redirect_uri:'http://127.0.0.1:5099/callback',code:callback.searchParams.get('code'),code_verifier:verifier}})).json()).access_token;
}
