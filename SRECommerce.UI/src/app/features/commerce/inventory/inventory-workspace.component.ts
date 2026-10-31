import { Component, DestroyRef, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { forkJoin, Observable } from 'rxjs';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
import { MerchantCatalogService } from '../../../core/services/merchant-catalog.service';
import { CommerceProduct } from '../../../core/models/commerce.models';
import { InventoryLookups, MerchantInventoryService, StockBalance, StockPurchase, StockReservation } from '../../../core/services/merchant-inventory.service';
@Component({selector:'app-inventory-workspace',standalone:true,imports:[CommonModule,FormsModule,RouterLink],templateUrl:'./inventory-workspace.component.html',styleUrl:'./inventory-workspace.component.scss'})
export class InventoryWorkspaceComponent {
  readonly session=inject(CommerceSessionService);private readonly api=inject(MerchantInventoryService);private readonly catalog=inject(MerchantCatalogService);private readonly route=inject(ActivatedRoute);private readonly destroy=inject(DestroyRef);
  readonly tenant=this.route.snapshot.paramMap.get('tenant')||'';readonly busy=signal(false);readonly error=signal('');readonly message=signal('');readonly lookups=signal<InventoryLookups>({branches:[],warehouses:[],items:[],suppliers:[]});readonly balances=signal<StockBalance[]>([]);readonly purchases=signal<StockPurchase[]>([]);readonly products=signal<CommerceProduct[]>([]);readonly reservation=signal<StockReservation|null>(null);readonly movements=signal<{action:number;onHandDelta:number;reservedDelta:number;damagedDelta:number;reason:string;occurredAt:string}[]>([]);
  supportReason='';branchCode='';branchName='';warehouseCode='';warehouseName='';branchId='';search='';productId='';variantId='';reorderLevel=2;
  warehouseId='';stockVariant='';quantity=1;lot='UNBATCHED';expires='';reason='';selectedBalance='';action=2;adjustment=1;adjustmentReason='';holdQuantity=1;reference='';
  supplierCode='';supplierName='';supplierEmail='';supplierId='';purchaseReference='';purchaseQuantity=1;unitCost=0;currency='INR';receivePurchase='';receiveLine='';receiveQuantity=1;receiveLot='UNBATCHED';receiveExpires='';
  private pendingKey:string|null=null;private pendingBody='';
  private readonly holdExpiries=new Map<string,string>();
  get canManage(){const a=this.session.access();return !!a&&(a.isPlatformAdmin||a.selectedTenantId===this.tenant&&['Owner','Manager'].includes(a.tenantRole||''));}
  get productVariants(){return this.products().find(p=>p.id===this.productId)?.variants||[];}
  get receiptLines(){return this.purchases().find(p=>p.id===this.receivePurchase)?.lines.filter(l=>l.ordered>l.received)||[];}
  constructor(){if(this.session.token&&this.canManage&&!this.session.access()?.isPlatformAdmin)this.reload();}
  async signIn(){try{await this.session.signIn(this.tenant,'inventory');}catch{this.error.set('Sign-in could not be started. Please retry.');}}
  private failure(value:unknown){const e=value as HttpErrorResponse;this.error.set(e.status===403?'Your role cannot manage inventory, or the module is disabled.':e.status===401?'Sign in again to continue.':e.status===409?'Stock changed or the operation conflicts with current data. Refresh before trying a new operation.':e.error?.title||'The request could not be completed. You can retry the same operation.');this.busy.set(false);}
  reload(){if(!this.canManage)return;if(this.session.access()?.isPlatformAdmin&&!this.supportReason.trim()){this.error.set('Enter a business support reason.');return;}this.busy.set(true);this.error.set('');forkJoin({lookups:this.api.lookups(this.tenant,this.supportReason),balances:this.api.balances(this.tenant,this.supportReason),purchases:this.api.purchases(this.tenant,this.supportReason)}).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>{this.lookups.set(r.lookups);this.balances.set(r.balances);this.purchases.set(r.purchases);this.warehouseId=this.warehouseId||r.lookups.warehouses.find(w=>w.active)?.id||'';this.branchId=this.branchId||r.lookups.branches.find(b=>b.active)?.id||'';this.busy.set(false);},error:e=>this.failure(e)});}
  private run<T>(request:Observable<T>,success:string,after?:(value:T)=>void){if(this.busy())return;this.busy.set(true);this.error.set('');this.message.set('');request.pipe(takeUntilDestroyed(this.destroy)).subscribe({next:value=>{this.pendingKey=null;this.pendingBody='';after?.(value);this.busy.set(false);this.message.set(success);this.reload();},error:e=>this.failure(e)});}
  private operation(value:object){const body=JSON.stringify(value);if(!this.pendingKey||this.pendingBody!==body){this.pendingKey=crypto.randomUUID();this.pendingBody=body;}return{operationId:this.pendingKey,...value};}
  private expiry(value:string){return value?new Date(value+'T23:59:59').toISOString():null;}
  sku(id:string){return this.lookups().items.find(i=>i.id===id)?.sku||id;}
  warehouse(id:string){return this.lookups().warehouses.find(w=>w.id===id)?.name||id;}
  branch(){this.run(this.api.post(this.tenant,'branches',{code:this.branchCode,name:this.branchName},this.supportReason),'Branch created.',()=>{this.branchCode='';this.branchName='';});}
  warehouseCreate(){this.run(this.api.post(this.tenant,'warehouses',{branchId:this.branchId,code:this.warehouseCode,name:this.warehouseName},this.supportReason),'Warehouse created.',()=>{this.warehouseCode='';this.warehouseName='';});}
  findProducts(){if(this.busy())return;this.catalog.products(this.tenant,1,this.supportReason,this.search).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:p=>{this.products.set(p.items.filter(i=>i.publishedRevision>0&&i.status!==2));this.productId='';this.variantId='';},error:e=>this.failure(e)});}
  register(){const existing=this.lookups().items.find(i=>i.id===this.variantId);this.run(this.api.post(this.tenant,'items',{productId:this.productId,variantId:this.variantId,reorderLevel:this.reorderLevel,version:existing?.version||null},this.supportReason),'Published SKU registered.');}
  receive(){const value=this.operation({warehouseId:this.warehouseId,variantId:this.stockVariant,action:0,quantity:this.quantity,reason:this.reason,lot:this.lot,expiresAt:this.expiry(this.expires)});this.run(this.api.post(this.tenant,'stock-changes',value,this.supportReason),'Stock receipt saved.');}
  adjust(){const balance=this.balances().find(b=>b.id===this.selectedBalance);if(!balance)return;const value=this.operation({warehouseId:balance.warehouseId,variantId:balance.variantId,batchId:balance.batchId,version:balance.version,action:Number(this.action),quantity:this.adjustment,reason:this.adjustmentReason});this.run(this.api.post(this.tenant,'stock-changes',value,this.supportReason),'Stock change saved.');}
  hold(){const value=this.operation({warehouseId:this.warehouseId,reference:this.reference,items:[{variantId:this.stockVariant,quantity:this.holdQuantity}]});const {operationId,...payload}=value;const expiresAt=this.holdExpiries.get(operationId)||new Date(Date.now()+15*60000).toISOString();this.holdExpiries.set(operationId,expiresAt);this.run(this.api.post<StockReservation>(this.tenant,'reservations',{id:operationId,...payload,expiresAt},this.supportReason),'Stock reserved.',r=>this.reservation.set(r));}
  close(commit:boolean){const r=this.reservation();if(r)this.run(this.api.post<StockReservation>(this.tenant,'reservations/'+r.id+(commit?'/commit':'/release'),{version:r.version},this.supportReason),commit?'Reservation committed. Physical stock deducted.':'Reservation released.',value=>this.reservation.set(value));}
  history(){const balance=this.balances().find(b=>b.id===this.selectedBalance);if(balance)this.apiHistory(balance.id);}
  private apiHistory(balance:string){this.api.movements(this.tenant,balance,this.supportReason).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:v=>this.movements.set(v),error:e=>this.failure(e)});}
  supplier(){this.run(this.api.post(this.tenant,'suppliers',{code:this.supplierCode,name:this.supplierName,contactEmail:this.supplierEmail||null},this.supportReason),'Supplier created.',()=>{this.supplierCode='';this.supplierName='';this.supplierEmail='';});}
  purchase(){this.run(this.api.post(this.tenant,'purchases',{supplierId:this.supplierId,warehouseId:this.warehouseId,reference:this.purchaseReference,currency:this.currency,lines:[{variantId:this.stockVariant,quantity:this.purchaseQuantity,unitCost:this.unitCost}]},this.supportReason),'Purchase draft created.');}
  purchaseState(p:StockPurchase,cancel:boolean){this.run(this.api.post(this.tenant,'purchases/'+p.id+(cancel?'/cancel':'/submit'),{version:p.version},this.supportReason),cancel?'Purchase cancelled. Received stock remains preserved.':'Purchase submitted.');}
  receivePo(){const p=this.purchases().find(p=>p.id===this.receivePurchase);if(!p)return;this.run(this.api.post(this.tenant,'purchases/'+p.id+'/receive',this.operation({version:p.version,lines:[{lineId:this.receiveLine,quantity:this.receiveQuantity,lot:this.receiveLot,expiresAt:this.expiry(this.receiveExpires)}]}),this.supportReason),'Purchase receipt saved.');}
}
