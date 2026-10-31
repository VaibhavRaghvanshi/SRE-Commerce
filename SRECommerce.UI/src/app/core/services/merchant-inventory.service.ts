import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { timeout } from 'rxjs';
export interface StockBranch { id:string;code:string;name:string;active:boolean;version:string; }
export interface StockWarehouse extends StockBranch { branchId:string; }
export interface StockItem { id:string;productId:string;sku:string;barcode:string|null;reorderLevel:number;version:string; }
export interface StockSupplier extends StockBranch { contactEmail:string|null; }
export interface InventoryLookups { branches:StockBranch[];warehouses:StockWarehouse[];items:StockItem[];suppliers:StockSupplier[]; }
export interface StockBalance { id:string;warehouseId:string;variantId:string;batchId:string;lot:string;expiresAt:string|null;onHand:number;reserved:number;damaged:number;available:number;version:string; }
export interface StockReservation { id:string;warehouseId:string;reference:string;status:number;expiresAt:string;items:{variantId:string;balanceId:string;quantity:number}[];version:string; }
export interface StockPurchase { id:string;supplierId:string;warehouseId:string;reference:string;currency:string;status:number;lines:{id:string;variantId:string;ordered:number;received:number;unitCost:number}[];version:string; }
@Injectable({providedIn:'root'})
export class MerchantInventoryService {
  private readonly http=inject(HttpClient);
  private base(tenant:string){return '/commerce/inventory/tenants/'+encodeURIComponent(tenant)+'/inventory';}
  private options(reason:string){return{headers:reason.trim()?new HttpHeaders({'X-Support-Reason':reason.trim()}):new HttpHeaders()};}
  lookups(tenant:string,reason:string){return this.http.get<InventoryLookups>(this.base(tenant)+'/lookups',this.options(reason)).pipe(timeout(15000));}
  balances(tenant:string,reason:string){return this.http.get<StockBalance[]>(this.base(tenant)+'/balances',this.options(reason)).pipe(timeout(15000));}
  purchases(tenant:string,reason:string){return this.http.get<StockPurchase[]>(this.base(tenant)+'/purchases',this.options(reason)).pipe(timeout(15000));}
  movements(tenant:string,balance:string,reason:string){return this.http.get<{action:number;onHandDelta:number;reservedDelta:number;damagedDelta:number;reason:string;occurredAt:string}[]>(this.base(tenant)+'/movements',{...this.options(reason),params:{balance}}).pipe(timeout(15000));}
  post<T>(tenant:string,path:string,value:unknown,reason:string){return this.http.post<T>(this.base(tenant)+'/'+path,value,this.options(reason));}
  availability(tenant:string,variant:string){return this.http.get<{variantId:string;available:number;lowStock:boolean;tracked:boolean}[]>('/commerce/inventory/public/tenants/'+encodeURIComponent(tenant)+'/inventory/availability',{params:{variants:variant}}).pipe(timeout(15000));}
}
