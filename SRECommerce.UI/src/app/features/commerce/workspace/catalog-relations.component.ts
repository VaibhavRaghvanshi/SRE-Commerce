import { CommonModule } from '@angular/common';
import { Component, DestroyRef, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { MerchantCatalogService, ProductChoice } from '../../../core/services/merchant-catalog.service';

interface Relation { productId: string; kind: string; }
@Component({ selector: 'app-catalog-relations', standalone: true, imports: [CommonModule, FormsModule], template: `
<section aria-label="Product recommendations"><h3>Related products and recommendations</h3><p>Choose products for related, upgrade or matching-item suggestions. Customers see only items currently published for their audience. Save this draft and publish to update the store.</p>
  <div class="search"><label>Find a product<input name="relation-query" [(ngModel)]="query" [ngModelOptions]="{standalone:true}" maxlength="100" /></label><button type="button" (click)="search()" [disabled]="searching()">Find recommendations</button></div>
  <p role="alert" *ngIf="error()">{{error()}}</p>
  <ul class="results"><li *ngFor="let row of results()"><span>{{row.name}} · {{row.slug}}</span><button type="button" (click)="add(row)" [disabled]="chosen(row.id) || value().length >= 40">{{chosen(row.id)?'Selected':'Add suggestion'}}</button></li></ul>
  <div class="search" *ngIf="total()"><button type="button" (click)="search(page - 1)" [disabled]="searching() || page <= 1">Previous results</button><span>Page {{page}}</span><button type="button" (click)="search(page + 1)" [disabled]="searching() || page * 24 >= total()">Next results</button></div>
  <ol><li *ngFor="let row of value()"><span>{{labels()[row.productId]?.name || 'Saved product · label unavailable'}}</span><label>Suggestion type<select [ngModel]="row.kind" [ngModelOptions]="{standalone:true}" (ngModelChange)="kind(row.productId,$event)"><option value="related">Related product</option><option value="upsell">Upgrade suggestion</option><option value="cross-sell">Matching item</option></select></label><button type="button" (click)="remove(row.productId)">Remove suggestion</button></li></ol><p *ngIf="!value().length">No product suggestions assigned.</p>
</section>`, styles: [`section{border-top:1px solid #ccd9cc;padding-top:1rem;margin:1rem 0;font-size:.85rem}p{line-height:1.6;color:#526558}.search,li{display:flex;gap:.6rem;align-items:center;flex-wrap:wrap;margin:.7rem 0}li>span{flex:1;min-width:130px;overflow-wrap:anywhere}input,select{display:block;padding:.5rem;border:1px solid #9cb49c;border-radius:5px;max-width:100%;box-sizing:border-box}button{padding:.6rem;border:1px solid #9cb49c;border-radius:5px;background:#edf2e9;color:#1d3328;cursor:pointer}button:disabled{opacity:.5}.results{list-style:none;padding:0;max-height:280px;overflow:auto}ol{padding-left:1.2rem}[role=alert]{color:#96291e}`] })
export class CatalogRelationsComponent {
  readonly tenant = input.required<string>(); readonly productId = input(''); readonly supportReason = input('');
  readonly value = input.required<Relation[]>(); readonly valueChange = output<Relation[]>();
  readonly labels = signal<Record<string, ProductChoice>>({}); readonly results = signal<ProductChoice[]>([]);
  readonly total = signal(0); readonly searching = signal(false); readonly error = signal(''); query = ''; page = 1;
  private readonly api = inject(MerchantCatalogService); private readonly destroy = inject(DestroyRef); private searchRequest: Subscription | null = null;
  constructor() {
    effect(onCleanup => {
      const ids = [...new Set(this.value().map(row => row.productId))], tenant = this.tenant(), reason = this.supportReason(); this.labels.set({});
      if (!ids.length) return;
      const request = this.api.productChoices(tenant, ids, reason).subscribe({ next: rows => this.labels.set(Object.fromEntries(rows.map(row => [row.id, row]))), error: () => this.error.set('Saved suggestion labels could not be loaded. Your draft selections are preserved.') });
      onCleanup(() => request.unsubscribe());
    });
    effect(onCleanup => { this.tenant(); this.productId(); this.results.set([]); this.total.set(0); this.searching.set(false); onCleanup(() => this.searchRequest?.unsubscribe()); });
  }
  chosen(id: string) { return id === this.productId() || this.value().some(row => row.productId === id); }
  search(page = 1) {
    if (this.searching() || page < 1 || page > 10000) return; this.searching.set(true); this.error.set('');
    this.searchRequest = this.api.products(this.tenant(), page, this.supportReason(), this.query.trim()).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: result => { this.results.set(result.items.filter(row => row.id !== this.productId())); this.total.set(result.total); this.page = page; this.searching.set(false); },
      error: () => { this.error.set('Product suggestions could not be loaded. Try again.'); this.searching.set(false); }
    });
  }
  add(row: ProductChoice) { if (!this.chosen(row.id) && this.value().length < 40) this.valueChange.emit([...this.value(), { productId: row.id, kind: 'related' }]); }
  remove(id: string) { this.valueChange.emit(this.value().filter(row => row.productId !== id)); }
  kind(id: string, kind: string) { if (['related', 'upsell', 'cross-sell'].includes(kind)) this.valueChange.emit(this.value().map(row => row.productId === id ? { ...row, kind } : row)); }
}
