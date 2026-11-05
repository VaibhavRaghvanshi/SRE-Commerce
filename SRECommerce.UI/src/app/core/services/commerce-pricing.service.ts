import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { timeout } from 'rxjs';
import { CommerceSessionService } from './commerce-session.service';
export interface CommerceQuote {
  id:string;tenantId:string;userId:string|null;currency:string;decimalPlaces:number;expiresAt:string;couponId:string|null;
  calculation:{promotionId:string|null;promotionCode:string|null;subtotal:number;discount:number;net:number;tax:number;total:number;lines:{variantId:string;sku:string;quantity:number;mrp:number;unitPrice:number;subtotal:number;discount:number;net:number;tax:number;total:number;taxRate:number;taxInclusive:boolean}[]};
}
@Injectable({providedIn:'root'})
export class CommercePricingService {
  private readonly http=inject(HttpClient);
  private readonly session=inject(CommerceSessionService);
  quote(tenant:string,variant:string,quantity:number,couponCode:string){
    const audience=this.session.token&&this.session.access()?.selectedTenantId===tenant?'':'public/';
    return this.http.post<CommerceQuote>('/commerce/pricing/'+audience+'tenants/'+encodeURIComponent(tenant)+'/pricing/quotes',{id:crypto.randomUUID(),items:[{variantId:variant,quantity}],couponCode:couponCode.trim()||null}).pipe(timeout(15000));
  }
}
