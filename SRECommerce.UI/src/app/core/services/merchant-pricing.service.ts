import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { timeout } from 'rxjs';
export interface PriceSettings { currency:string;decimalPlaces:number;version:string; }
export interface PriceTax { id:string;code:string;name:string;rate:number;inclusive:boolean;active:boolean;version:string; }
export interface PriceGroup { id:string;code:string;name:string;active:boolean;version:string; }
export interface PriceList { id:string;code:string;name:string;currency:string;audience:number;customerGroupId:string|null;priority:number;startsAt:string;endsAt:string;active:boolean;version:string; }
export interface PriceRow { id:string;priceListId:string;productId:string;variantId:string;sku:string;isBundle:boolean;taxPolicyId:string;minimumQuantity:number;mrp:number;sellingPrice:number;costPrice:number|null;active:boolean;version:string; }
export interface PricePromotion { id:string;code:string;name:string;kind:number;value:number;buyQuantity:number;getQuantity:number;minimumSpend:number;maximumDiscount:number|null;variantIds:string[];customerGroupId:string|null;b2BOnly:boolean;automatic:boolean;priority:number;startsAt:string;endsAt:string;active:boolean;version:string; }
export interface PriceCoupon { id:string;code:string;promotionId:string;globalLimit:number;perCustomerLimit:number;active:boolean;version:string; }
export interface PriceLookups { currency:string;settings:PriceSettings|null;taxes:PriceTax[];groups:PriceGroup[];lists:PriceList[];promotions:PricePromotion[];coupons:PriceCoupon[]; }
@Injectable({providedIn:'root'})
export class MerchantPricingService {
  private readonly http=inject(HttpClient);
  private base(tenant:string){return '/commerce/pricing/tenants/'+encodeURIComponent(tenant)+'/pricing';}
  private options(reason:string){return{headers:reason.trim()?new HttpHeaders({'X-Support-Reason':reason.trim()}):new HttpHeaders()};}
  lookups(tenant:string,reason:string){return this.http.get<PriceLookups>(this.base(tenant)+'/lookups',this.options(reason)).pipe(timeout(15000));}
  prices(tenant:string,page:number,search:string,reason:string){return this.http.get<{items:PriceRow[];page:number;pageSize:number;total:number}>(this.base(tenant)+'/prices',{...this.options(reason),params:{page,search}}).pipe(timeout(15000));}
  save<T>(tenant:string,path:string,value:unknown,reason:string,id:string|null=null){return id?this.http.put<T>(this.base(tenant)+'/'+path+'/'+id,value,this.options(reason)):this.http.post<T>(this.base(tenant)+'/'+path,value,this.options(reason));}
  settings(tenant:string,value:unknown,reason:string){return this.http.put(this.base(tenant)+'/settings',value,this.options(reason));}
  members(tenant:string){return this.http.get<{userId:string;displayName?:string;role:string;isActive:boolean}[]>('/commerce/identity/tenants/'+encodeURIComponent(tenant)+'/memberships').pipe(timeout(15000));}
  memberGroup(tenant:string,user:string,reason:string){return this.http.get<{groupId:string|null;version:string|null}>(this.base(tenant)+'/group-members/'+user,this.options(reason)).pipe(timeout(15000));}
  assign(tenant:string,user:string,group:string,version:string|null,reason:string){return this.http.put<{userId:string;groupId:string;version:string}>(this.base(tenant)+'/group-members/'+user,{groupId:group,active:true,version},this.options(reason));}
  history(tenant:string,id:string,reason:string){return this.http.get<{id:string;before:string;after:string;changedAt:string}[]>(this.base(tenant)+'/prices/'+id+'/history',this.options(reason)).pipe(timeout(15000));}
}
