import { Component, DestroyRef, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of, switchMap, tap, Subject } from 'rxjs';
import { MerchantInventoryService } from '../../../core/services/merchant-inventory.service';
import { CommerceCatalogService } from '../../../core/services/commerce-catalog.service';
import { CommerceProduct, CommerceTenant } from '../../../core/models/commerce.models';
@Component({selector:'app-commerce-product',standalone:true,imports:[CommonModule,RouterLink],template:`
<main class="detail"><a [routerLink]="['/stores',tenant()?.slug || storeSlug]">← Back to {{ tenant()?.displayName || 'store' }}</a>
<p role="status" *ngIf="loading()">Loading product…</p><p role="alert" *ngIf="error()">{{error()}}</p>
<section *ngIf="product() as item"><div class="images"><img *ngFor="let media of item.media" [src]="media.url" [alt]="media.alt" /><div class="placeholder" *ngIf="!item.media.length">{{ item.name }}</div></div>
<div><p class="eyebrow">{{ tenant()?.displayName }}</p><h1>{{ item.name }}</h1><p class="description">{{ item.description }}</p>
<h2>Available variants</h2><div class="variants"><button *ngFor="let variant of item.variants" type="button" [class.selected]="selected() === variant.id" (click)="choose(variant.id)">{{ variant.sku }}<span *ngFor="let option of variant.options | keyvalue"> · {{option.value}}</span></button></div>
<p class="availability" role="status">{{stockLoading()?'Checking stock…':available()===null?'Stock availability is temporarily unavailable.':available()===0?'Out of stock.':available()+' units available.'}} Price is temporarily unavailable.</p>
<h2 *ngIf="(item.attributes | keyvalue).length">Product details</h2><dl><ng-container *ngFor="let attribute of item.attributes | keyvalue"><dt>{{attribute.key}}</dt><dd>{{attribute.value}}</dd></ng-container><ng-container *ngFor="let spec of item.specifications | keyvalue"><dt>{{spec.key}}</dt><dd>{{spec.value}}</dd></ng-container></dl>
</div></section></main>`,styles:[`:host{display:block;background:#f7f8f4;color:#1d3328;min-height:100vh}.detail{max-width:1200px;margin:auto;padding:35px 24px}.detail>a{font-size:13px;text-underline-offset:5px}section{display:grid;grid-template-columns:1fr 1fr;gap:50px;margin-top:35px}.images img{width:100%;border-radius:12px;margin-bottom:15px}.placeholder{aspect-ratio:1;background:#e4eadf;display:grid;place-items:center;font:30px Georgia,serif;padding:25px;border-radius:12px}h1{font:42px/1.15 Georgia,serif;margin:14px 0 25px}h2{font-size:16px;margin-top:30px}.eyebrow{text-transform:uppercase;font-size:11px;letter-spacing:2px}.description{line-height:1.8;color:#526558;white-space:pre-line}.variants{display:flex;flex-wrap:wrap;gap:8px}.variants button{background:white;color:#1d3328;border:1px solid #b8c7b9;padding:12px;border-radius:6px;cursor:pointer}.variants button.selected{border:2px solid #204b35}.availability{font-size:13px;line-height:1.7;color:#536457;padding:15px;background:#e9eee5;border-radius:8px}dl{display:grid;grid-template-columns:1fr 2fr;font-size:14px;gap:12px}dt{text-transform:capitalize}dd{margin:0;color:#526558}@media(max-width:650px){section{grid-template-columns:1fr;gap:22px}h1{font-size:34px}}`]})
export class CommerceProductComponent {
  private readonly api=inject(CommerceCatalogService);
  private readonly route=inject(ActivatedRoute);
  private readonly destroy=inject(DestroyRef);
  private readonly inventory=inject(MerchantInventoryService);private readonly stockRequests=new Subject<string>();readonly available=signal<number|null>(null);readonly stockLoading=signal(false);
  readonly product=signal<CommerceProduct|null>(null);readonly tenant=signal<CommerceTenant|null>(null);readonly selected=signal('');readonly loading=signal(true);readonly error=signal('');storeSlug='';
  constructor(){this.stockRequests.pipe(switchMap(variant=>{const tenant=this.tenant();return tenant?.modules.includes('inventory')&&variant?this.inventory.availability(tenant.tenantId,variant).pipe(catchError(()=>of(null))):of(null);}),takeUntilDestroyed(this.destroy)).subscribe(value=>{this.available.set(value?.[0]?.tracked?value[0].available:null);this.stockLoading.set(false);});this.route.paramMap.pipe(tap(()=>{this.loading.set(true);this.error.set('');this.product.set(null);this.choose('');}),switchMap(params=>{this.storeSlug=params.get('slug')||'';return this.api.tenant(this.storeSlug).pipe(switchMap(tenant=>{this.tenant.set(tenant);return this.api.product(tenant.tenantId,params.get('product')||'');}),catchError(()=>{this.error.set('This product is unavailable or has not been published.');return of(null);}));}),takeUntilDestroyed(this.destroy)).subscribe(value=>{this.product.set(value);this.choose(value?.variants[0]?.id||'');this.loading.set(false);});}
  choose(variant:string){this.selected.set(variant);this.available.set(null);this.stockLoading.set(!!variant);this.stockRequests.next(variant);}
}
