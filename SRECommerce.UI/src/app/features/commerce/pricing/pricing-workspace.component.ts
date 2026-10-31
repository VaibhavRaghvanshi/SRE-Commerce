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
import { MerchantPricingService, PriceCoupon, PriceGroup, PriceList, PriceLookups, PricePromotion, PriceRow, PriceTax } from '../../../core/services/merchant-pricing.service';
@Component({selector:'app-pricing-workspace',standalone:true,imports:[CommonModule,FormsModule,RouterLink],templateUrl:'./pricing-workspace.component.html',styleUrl:'./pricing-workspace.component.scss'})
export class PricingWorkspaceComponent {
  readonly session=inject(CommerceSessionService);private readonly api=inject(MerchantPricingService);private readonly catalog=inject(MerchantCatalogService);private readonly route=inject(ActivatedRoute);private readonly destroy=inject(DestroyRef);
  readonly tenant=this.route.snapshot.paramMap.get('tenant')||'';readonly busy=signal(false);readonly error=signal('');readonly message=signal('');readonly lookups=signal<PriceLookups>({currency:'',settings:null,taxes:[],groups:[],lists:[],promotions:[],coupons:[]});readonly prices=signal<PriceRow[]>([]);readonly total=signal(0);readonly page=signal(1);readonly products=signal<CommerceProduct[]>([]);readonly history=signal<{before:string;after:string;changedAt:string}[]>([]);readonly members=signal<{userId:string;displayName?:string;role:string;isActive:boolean}[]>([]);
  supportReason='';currency='';decimalPlaces=2;productSearch='';priceSearch='';productId='';variantId='';priceId:string|null=null;priceVersion:string|null=null;priceListId='';taxId='';minimumQuantity=1;mrp=0;sellingPrice=0;costPrice:number|null=null;priceActive=true;
  taxEditor:{id:string|null;code:string;name:string;rate:number;inclusive:boolean;active:boolean;version:string|null}={id:null,code:'',name:'',rate:0,inclusive:false,active:true,version:null};
  groupEditor:{id:string|null;code:string;name:string;active:boolean;version:string|null}={id:null,code:'',name:'',active:true,version:null};
  listEditor=this.blankList();promotionEditor=this.blankPromotion();couponEditor:{id:string|null;code:string;promotionId:string;globalLimit:number;perCustomerLimit:number;active:boolean;version:string|null}={id:null,code:'',promotionId:'',globalLimit:100,perCustomerLimit:1,active:true,version:null};
  memberId='';memberGroup='';private readonly memberVersions=new Map<string,string>();
  get canManage(){const a=this.session.access();return !!a&&(a.isPlatformAdmin||a.selectedTenantId===this.tenant&&['Owner','Manager'].includes(a.tenantRole||''));}
  get isOwner(){return this.session.access()?.isPlatformAdmin||this.session.access()?.tenantRole==='Owner';}
  get variants(){return this.products().find(p=>p.id===this.productId)?.variants||[];}
  get promotionTargets(){return [...new Map(this.prices().map(p=>[p.variantId,{id:p.variantId,sku:p.sku}])).values()];}
  constructor(){if(this.session.token&&this.canManage&&!this.session.access()?.isPlatformAdmin)this.reload();}
  private local(value:Date){return new Date(value.getTime()-value.getTimezoneOffset()*60000).toISOString().slice(0,16);}
  private blankList(){return{id:null as string|null,code:'',name:'',audience:0,customerGroupId:null as string|null,priority:0,startsAt:this.local(new Date()),endsAt:this.local(new Date(Date.now()+30*86400000)),active:true,version:null as string|null};}
  private blankPromotion(){return{id:null as string|null,code:'',name:'',kind:0,value:10,buyQuantity:2,getQuantity:1,minimumSpend:0,maximumDiscount:null as number|null,variantIds:[] as string[],customerGroupId:null as string|null,b2BOnly:false,automatic:true,priority:0,startsAt:this.local(new Date()),endsAt:this.local(new Date(Date.now()+30*86400000)),active:true,version:null as string|null};}
  async signIn(){try{await this.session.signIn(this.tenant,'pricing');}catch{this.error.set('Sign-in could not be started. Please retry.');}}
  private failure(value:unknown){const e=value as HttpErrorResponse;this.error.set(e.status===401?'Sign in again to continue.':e.status===403?'Your role cannot manage pricing, or this module is disabled.':e.status===409?'The price or configuration changed, or conflicts with current data. Refresh before retrying.':e.error?.title||'The pricing request could not be completed. Please retry.');this.busy.set(false);}
  reload(page=this.page()){if(!this.canManage)return;if(this.session.access()?.isPlatformAdmin&&!this.supportReason.trim()){this.error.set('Enter the business support reason.');return;}this.busy.set(true);this.error.set('');forkJoin({lookups:this.api.lookups(this.tenant,this.supportReason),prices:this.api.prices(this.tenant,page,this.priceSearch,this.supportReason)}).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>{this.lookups.set(r.lookups);this.prices.set(r.prices.items);this.page.set(r.prices.page);this.total.set(r.prices.total);this.currency=r.lookups.currency;this.decimalPlaces=r.lookups.settings?.decimalPlaces??2;this.busy.set(false);},error:e=>this.failure(e)});}
  private run<T>(request:Observable<T>,message:string,after?:(value:T)=>void){if(this.busy())return;this.busy.set(true);this.error.set('');this.message.set('');request.pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>{after?.(r);this.busy.set(false);this.message.set(message);this.reload();},error:e=>this.failure(e)});}
  saveSettings(){this.run(this.api.settings(this.tenant,{currency:this.currency,decimalPlaces:this.decimalPlaces,version:this.lookups().settings?.version||null},this.supportReason),'Currency settings saved.');}
  editTax(t:PriceTax){this.taxEditor={...t};}newTax(){this.taxEditor={id:null,code:'',name:'',rate:0,inclusive:false,active:true,version:null};}
  saveTax(){const{id,...value}=this.taxEditor;this.run(this.api.save(this.tenant,'tax-policies',value,this.supportReason,id),'Tax policy saved.');}
  editGroup(g:PriceGroup){this.groupEditor={...g};}newGroup(){this.groupEditor={id:null,code:'',name:'',active:true,version:null};}
  saveGroup(){const{id,...value}=this.groupEditor;this.run(this.api.save(this.tenant,'groups',value,this.supportReason,id),'Customer price group saved.');}
  loadMembers(){this.api.members(this.tenant).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:m=>this.members.set(m.filter(i=>i.isActive&&['Customer','B2BBuyer'].includes(i.role))),error:e=>this.failure(e)});}
  loadMemberGroup(){const user=this.memberId;if(!user)return;this.api.memberGroup(this.tenant,user,this.supportReason).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:m=>{if(this.memberId!==user)return;this.memberGroup=m.groupId||'';if(m.version)this.memberVersions.set(user,m.version);else this.memberVersions.delete(user);},error:e=>{if(this.memberId===user)this.failure(e);}});}
  assignGroup(){this.run(this.api.assign(this.tenant,this.memberId,this.memberGroup,this.memberVersions.get(this.memberId)||null,this.supportReason),'Customer price group assigned.',r=>this.memberVersions.set(r.userId,r.version));}
  editList(l:PriceList){const{currency,...value}=l;this.listEditor={...value,startsAt:this.local(new Date(l.startsAt)),endsAt:this.local(new Date(l.endsAt))};}newList(){this.listEditor=this.blankList();}
  saveList(){const{id,...value}=this.listEditor;this.run(this.api.save(this.tenant,'price-lists',{...value,currency:this.currency,audience:Number(value.audience),customerGroupId:Number(value.audience)===2?value.customerGroupId:null,startsAt:new Date(value.startsAt).toISOString(),endsAt:new Date(value.endsAt).toISOString()},this.supportReason,id),'Price list saved.');}
  findProducts(){if(this.busy())return;this.catalog.products(this.tenant,1,this.supportReason,this.productSearch).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>{this.products.set(r.items.filter(p=>p.status!==2&&p.publishedRevision>0));this.productId='';this.variantId='';},error:e=>this.failure(e)});}
  newPrice(){this.priceId=null;this.priceVersion=null;this.productId='';this.variantId='';this.minimumQuantity=1;this.mrp=0;this.sellingPrice=0;this.costPrice=null;this.priceActive=true;}
  editPrice(p:PriceRow){this.priceId=p.id;this.priceVersion=p.version;this.productId=p.productId;this.variantId=p.variantId;this.priceListId=p.priceListId;this.taxId=p.taxPolicyId;this.minimumQuantity=p.minimumQuantity;this.mrp=p.mrp;this.sellingPrice=p.sellingPrice;this.costPrice=p.costPrice;this.priceActive=p.active;}
  savePrice(){this.run(this.api.save(this.tenant,'prices',{priceListId:this.priceListId,productId:this.productId,variantId:this.variantId,taxPolicyId:this.taxId,minimumQuantity:this.minimumQuantity,mrp:this.mrp,sellingPrice:this.sellingPrice,costPrice:this.costPrice,active:this.priceActive,version:this.priceVersion},this.supportReason,this.priceId),'SKU price saved.',()=>this.newPrice());}
  showHistory(p:PriceRow){this.api.history(this.tenant,p.id,this.supportReason).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:h=>this.history.set(h),error:e=>this.failure(e)});}
  historyMoney(document:string,field:'sellingPrice'|'costPrice'){try{const value=JSON.parse(document);const currency=this.lookups().lists.find(l=>l.id===value?.priceListId)?.currency||this.currency;return typeof value?.[field]==='number'?currency+' '+value[field].toFixed(4):'—';}catch{return '—';}}
  editPromotion(p:PricePromotion){this.promotionEditor={...p,startsAt:this.local(new Date(p.startsAt)),endsAt:this.local(new Date(p.endsAt))};}newPromotion(){this.promotionEditor=this.blankPromotion();}
  savePromotion(){const{id,...value}=this.promotionEditor;this.run(this.api.save(this.tenant,'promotions',{...value,kind:Number(value.kind),value:Number(value.kind)===3?0:value.value,startsAt:new Date(value.startsAt).toISOString(),endsAt:new Date(value.endsAt).toISOString()},this.supportReason,id),'Promotion saved.');}
  editCoupon(c:PriceCoupon){this.couponEditor={...c};}newCoupon(){this.couponEditor={id:null,code:'',promotionId:'',globalLimit:100,perCustomerLimit:1,active:true,version:null};}
  saveCoupon(){const{id,...value}=this.couponEditor;this.run(this.api.save(this.tenant,'coupons',value,this.supportReason,id),'Coupon saved.');}
}
