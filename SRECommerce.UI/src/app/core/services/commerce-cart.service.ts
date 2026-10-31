import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom, timeout } from 'rxjs';
import { CommerceSessionService } from './commerce-session.service';
import { CommerceCart, CommerceCartPreview, CommercePreparation } from '../models/commerce-cart.models';
interface GuestCart {id:string;key:string;expiresAt:string;}
interface PendingWrite {body:Record<string,unknown>;url:string;method:'PUT'|'POST';headers:Record<string,string>;}
@Injectable({providedIn:'root'})
export class CommerceCartService {
 private readonly http=inject(HttpClient);private readonly session=inject(CommerceSessionService);
 private readonly states=signal<Record<string,CommerceCart>>({});private readonly guests=new Map<string,GuestCart>();private readonly writes=new Map<string,PendingWrite>();private readonly merges=new Map<string,PendingWrite>();private queue:Promise<unknown>=Promise.resolve();
 private customer(tenant:string){return !!this.session.token&&this.session.access()?.selectedTenantId===tenant;}
 private stateKey(tenant:string){return tenant+':'+(this.customer(tenant)?this.session.access()!.userId:'guest');}
 private remember(tenant:string,cart:CommerceCart){this.states.update(state=>({...state,[this.stateKey(tenant)]:cart}));const guest=this.guests.get(tenant);if(cart.guest&&guest){guest.expiresAt=cart.expiresAt;this.persist(tenant,guest);}return cart;}
 view(tenant:string){return this.states()[this.stateKey(tenant)]||null;}
 private storage(tenant:string){return 'srecommerce.cart.v1.'+tenant;}
 private persist(tenant:string,value:GuestCart|null){if(value)this.guests.set(tenant,value);else this.guests.delete(tenant);try{if(typeof localStorage!=='undefined'){if(value)localStorage.setItem(this.storage(tenant),JSON.stringify(value));else localStorage.removeItem(this.storage(tenant));}}catch{/* Session memory remains usable if storage is disabled. */}}
 private guest(tenant:string,create=true):GuestCart|null {
  let value=this.guests.get(tenant)||null;
  try{if(!value&&typeof localStorage!=='undefined'){const stored=localStorage.getItem(this.storage(tenant));if(stored)value=JSON.parse(stored);}}catch{value=null;}
  if(value&&(!/^[a-f0-9-]{36}$/i.test(value.id)||!/^[A-Za-z0-9_-]{43}$/.test(value.key)||!(Date.parse(value.expiresAt)>Date.now()))){this.persist(tenant,null);value=null;}
  if(!value&&create){const bytes=crypto.getRandomValues(new Uint8Array(32));value={id:crypto.randomUUID(),key:btoa(String.fromCharCode(...bytes)).replace(/=+$/,'').split('+').join('-').split('/').join('_'),expiresAt:new Date(Date.now()+7*86400000).toISOString()};this.persist(tenant,value);}
  if(value)this.guests.set(tenant,value);return value;
 }
 private base(tenant:string,guest:boolean){return '/commerce/cart/'+(guest?'public/':'')+'tenants/'+encodeURIComponent(tenant)+'/cart/carts';}
 private headers(tenant:string,guest:boolean):Record<string,string>{return guest?{'X-Cart-Key':this.guest(tenant)!.key}:{};}
 private request<T>(url:string,method:'GET'|'POST'|'PUT',body?:unknown,headers:Record<string,string>={}){return firstValueFrom(this.http.request<T>(method,url,{body,headers}).pipe(timeout(15000)));}
 private serial<T>(work:()=>Promise<T>):Promise<T>{const next=this.queue.then(work,work);this.queue=next.catch(()=>undefined);return next;}
 private uncertain(error:unknown){return !(error instanceof HttpErrorResponse)||error.status===0||error.status>=500||error.status===429;}
 private async ensure(tenant:string):Promise<CommerceCart>{
  if(this.customer(tenant)){
   let cart=await this.request<CommerceCart>(this.base(tenant,false),'POST',{id:crypto.randomUUID()});const guest=this.guest(tenant,false);
   if(guest){
    let pending=this.merges.get(tenant);
    if(!pending){
     let source:CommerceCart;
     try{source=await this.request<CommerceCart>(this.base(tenant,true)+'/'+guest.id,'GET',undefined,{'X-Cart-Key':guest.key});}
     catch(error){if(error instanceof HttpErrorResponse&&[401,404,409,410].includes(error.status)){this.persist(tenant,null);return this.remember(tenant,cart);}throw error;}
     pending={url:this.base(tenant,false)+'/'+cart.id+'/merge',method:'POST',headers:{'X-Cart-Key':guest.key},body:{operationId:crypto.randomUUID(),guestCartId:source.id,guestVersion:source.version,version:cart.version}};this.merges.set(tenant,pending);
    }
    try{cart=await this.request<CommerceCart>(pending.url,pending.method,pending.body,pending.headers);this.merges.delete(tenant);this.persist(tenant,null);}
    catch(error){if(!this.uncertain(error))this.merges.delete(tenant);throw error;}
   }
   return this.remember(tenant,cart);
  }
  const guest=this.guest(tenant)!;
  try{return this.remember(tenant,await this.request<CommerceCart>(this.base(tenant,true),'POST',{id:guest.id},{'X-Cart-Key':guest.key}));}
  catch(error){if(error instanceof HttpErrorResponse&&[404,409,410].includes(error.status)){this.persist(tenant,null);const fresh=this.guest(tenant)!;return this.remember(tenant,await this.request<CommerceCart>(this.base(tenant,true),'POST',{id:fresh.id},{'X-Cart-Key':fresh.key}));}throw error;}
 }
 load(tenant:string){return this.serial(()=>this.ensure(tenant));}
 private mutate(tenant:string,label:string,make:(cart:CommerceCart,guest:boolean)=>PendingWrite){return this.serial(async()=>{
  const key=this.stateKey(tenant)+':'+label;let pending=this.writes.get(key);
  if(!pending){const cart=await this.ensure(tenant);pending=make(cart,cart.guest);this.writes.set(key,pending);}
  try{const cart=await this.request<CommerceCart>(pending.url,pending.method,pending.body,pending.headers);this.writes.delete(key);return this.remember(tenant,cart);}catch(error){if(!this.uncertain(error))this.writes.delete(key);throw error;}
 });}
 add(tenant:string,variant:string,quantity:number){return this.mutate(tenant,'add:'+variant+':'+quantity,(cart,guest)=>({url:this.base(tenant,guest)+'/'+cart.id+'/items',method:'PUT',headers:this.headers(tenant,guest),body:{operationId:crypto.randomUUID(),variantId:variant,quantity:quantity+(cart.items.find(i=>i.variantId===variant)?.quantity||0),version:cart.version}}));}
 set(tenant:string,variant:string,quantity:number){return this.mutate(tenant,'set:'+variant+':'+quantity,(cart,guest)=>({url:this.base(tenant,guest)+'/'+cart.id+'/items',method:'PUT',headers:this.headers(tenant,guest),body:{operationId:crypto.randomUUID(),variantId:variant,quantity,version:cart.version}}));}
 remove(tenant:string,variant:string){return this.mutate(tenant,'remove:'+variant,(cart,guest)=>({url:this.base(tenant,guest)+'/'+cart.id+'/items/'+variant+'/remove',method:'POST',headers:this.headers(tenant,guest),body:{operationId:crypto.randomUUID(),version:cart.version}}));}
 preview(tenant:string,coupon:string){return this.serial(async()=>{const cart=await this.ensure(tenant);return this.request<CommerceCartPreview>(this.base(tenant,cart.guest)+'/'+cart.id+'/preview','POST',{id:crypto.randomUUID(),couponCode:coupon.trim()||null},this.headers(tenant,cart.guest));});}
 prepare(tenant:string,coupon:string){return this.serial(async()=>{const cart=await this.ensure(tenant);return this.request<CommercePreparation>(this.base(tenant,cart.guest)+'/'+cart.id+'/prepare','POST',{id:crypto.randomUUID(),version:cart.version,couponCode:coupon.trim()||null},this.headers(tenant,cart.guest));});}
}
