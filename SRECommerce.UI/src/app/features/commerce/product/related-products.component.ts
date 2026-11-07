import { CommonModule } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommerceCatalogService } from '../../../core/services/commerce-catalog.service';
import { StoreLanguageService } from '../../../core/services/store-language.service';
import { CommerceProduct } from '../../../core/models/commerce.models';
import { CommerceImageDirective } from '../../../core/directives/commerce-image.directive';

@Component({ selector: 'app-related-products', standalone: true, imports: [CommonModule, RouterLink, CommerceImageDirective], template: `
<aside *ngIf="items().length" aria-label="Related products"><h2>{{locale.text('You may also like')}}</h2><div class="related-grid"><article *ngFor="let item of items()">
  <a [routerLink]="['/stores', store(), 'products', item.product.slug]"><img *ngIf="item.product.media.length" [commerceImage]="item.product.media[0].url" [imageTenant]="tenant()" [alt]="item.product.media[0].alt || locale.product(item.product).name" loading="lazy" /><span class="kind">{{locale.text(label(item.kind))}}</span><h3>{{locale.product(item.product).name}}</h3><span class="detail">{{locale.text('View details')}} →</span></a>
</article></div></aside>`, styles: [`aside{margin-top:3rem;border-top:1px solid var(--color-border,#d8e0d8);padding-top:1rem}h2{font-size:1.2rem}.related-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:1rem}article{background:var(--color-surface,#fff);border:1px solid var(--color-border,#d8e0d8);border-radius:10px;overflow:hidden;min-width:0}a{display:block;padding:1rem;color:inherit;text-decoration:none}img{width:100%;aspect-ratio:1;object-fit:cover;border-radius:6px}.kind,.detail{display:block;font-size:.75rem;margin-top:.8rem;color:var(--color-muted-text,#526558)}h3{font-size:1rem;line-height:1.4;overflow-wrap:anywhere}@media(max-width:650px){.related-grid{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:380px){.related-grid{grid-template-columns:1fr}}`] })
export class RelatedProductsComponent {
  readonly locale = inject(StoreLanguageService);
  readonly tenant = input.required<string>(); readonly store = input.required<string>(); readonly product = input.required<CommerceProduct>();
  readonly items = signal<{ kind: string; product: CommerceProduct }[]>([]);
  private readonly api = inject(CommerceCatalogService);
  constructor() {
    effect(onCleanup => {
      const tenant = this.tenant(), product = this.product();
      this.api.member(tenant); // Reload eligibility when the session audience changes.
      this.items.set([]);
      const relations = (product.relationships || []).filter(row => row.productId !== product.id).slice(0, 40);
      const ids = [...new Set(relations.map(row => row.productId))]; if (!ids.length) return;
      const subscription = this.api.shoppingProductsById(tenant, ids).subscribe({ next: products => {
        this.items.set(relations.flatMap(row => { const match = products.find(item => item.id === row.productId); return match ? [{ kind: row.kind, product: match }] : []; }));
      }, error: () => this.items.set([]) });
      onCleanup(() => subscription.unsubscribe());
    });
  }
  label(kind: string) { return kind === 'upsell' ? 'Consider an upgrade' : kind === 'cross-sell' ? 'Pairs well with this' : 'Related item'; }
}
