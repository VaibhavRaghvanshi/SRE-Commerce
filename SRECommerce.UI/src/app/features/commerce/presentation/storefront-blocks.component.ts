import { CommonModule } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { catchError, of, timeout } from 'rxjs';
import { CommerceLookup, CommerceProduct } from '../../../core/models/commerce.models';
import { CommerceCatalogService } from '../../../core/services/commerce-catalog.service';
import { Layout, LayoutLink, Placement } from '../../../core/services/commerce-presentation.service';

@Component({
  selector: 'app-storefront-blocks', standalone: true, imports: [CommonModule, RouterLink],
  template: `
    <nav *ngIf="links().length" aria-label="Store navigation"><ng-container *ngFor="let l of links()">
      <a *ngIf="internal(l.href) as path; else external" [routerLink]="path">{{l.label}}</a>
      <ng-template #external><a *ngIf="secure(l.href)" [href]="l.href" rel="noopener noreferrer">{{l.label}}</a></ng-template>
    </ng-container></nav>
    <ng-container *ngIf="!footer()">
      <aside *ngFor="let b of banners()" class="block">
        <h2>{{b.title}}</h2><img *ngIf="image(b.imageUrl)" [src]="b.imageUrl" [alt]="b.alt" loading="lazy" />
        <a *ngIf="internal(b.link) as path; else externalBanner" [routerLink]="path">Explore {{b.title}}</a>
        <ng-template #externalBanner><a *ngIf="secure(b.link)" [href]="b.link" rel="noopener noreferrer">Explore {{b.title}}</a></ng-template>
      </aside>
      <section *ngFor="let s of sections()" class="block">
        <h2>{{s.title}}</h2><p *ngIf="s.text">{{s.text}}</p>
        <div class="cards" [style.--columns]="mobile() ? s.mobile.columns : s.desktop.columns">
          <ng-container *ngIf="s.kind === 'featured-categories'"><a *ngFor="let c of categoriesFor(s.references)" [routerLink]="['/stores',slug()]" [queryParams]="{ category: c.id }">{{c.name}}</a></ng-container>
          <ng-container *ngIf="['featured-products','new-arrivals','best-sellers','promotion'].includes(s.kind)">
            <a *ngFor="let p of productsFor(s.references)" [routerLink]="['/stores',slug(),'products',p.slug]">
              <img *ngIf="p.media.length && image(p.media[0].url)" [src]="p.media[0].url" [alt]="p.media[0].alt" loading="lazy" />{{p.name}}
            </a>
          </ng-container>
        </div>
      </section>
    </ng-container>
    <footer *ngIf="footer()"><p>{{layout().footer.copyright}}</p><p *ngIf="layout().footer.showContact && layout().footer.contactText">{{layout().footer.contactText}}</p></footer>
  `,
  styles: [`nav{display:flex;flex-wrap:wrap;gap:1rem;margin:.7rem 0}.block,footer{padding:1rem;margin:.7rem 0;border-radius:var(--border-radius,10px);background:var(--color-surface,#fff);color:var(--color-text,#19352c)}
    footer{background:var(--color-footer-background,#fff);color:var(--color-footer-text,#19352c)}a{color:inherit;text-underline-offset:.2em}
    .block p,footer p{white-space:pre-line}.cards{display:grid;grid-template-columns:repeat(var(--columns,2),minmax(0,1fr));gap:.6rem}.cards a{padding:.6rem;border:1px solid var(--color-border,#ccd9ce);border-radius:var(--border-radius,8px);overflow-wrap:anywhere}
    img{display:block;max-width:100%;height:auto;max-height:300px;object-fit:contain}`]
})
export class StorefrontBlocksComponent {
  readonly layout = input.required<Layout>(); readonly slug = input.required<string>(); readonly tenant = input.required<string>();
  readonly mobile = input(false); readonly categories = input<CommerceLookup[]>([]); readonly footer = input(false);
  private readonly catalog = inject(CommerceCatalogService);
  readonly products = signal<CommerceProduct[]>([]);
  readonly links = computed<LayoutLink[]>(() => this.layout().menus.filter(x => this.footer() ? x.position === 'footer' : x.position === (this.mobile() ? 'mobile' : 'header')).flatMap(x => x.links));
  readonly banners = computed(() => this.layout().banners.filter(x => this.place(x).visible).sort((a,b) => this.place(a).order - this.place(b).order));
  readonly sections = computed(() => this.layout().sections.filter(x => this.place(x).visible).sort((a,b) => this.place(a).order - this.place(b).order));
  constructor() {
    effect(onCleanup => {
      const ids = [...new Set(this.sections().filter(x => ['featured-products','new-arrivals','best-sellers','promotion'].includes(x.kind)).flatMap(x => x.references))].slice(0,50);
      const tenant = this.tenant(); this.products.set([]);
      if (this.footer() || !ids.length || typeof window === 'undefined') return;
      const sub = this.catalog.publicProductsById(tenant, ids).pipe(timeout(3000), catchError(() => of([]))).subscribe(x => this.products.set(x));
      onCleanup(() => sub.unsubscribe());
    });
  }
  private place(x: { desktop: Placement; mobile: Placement }) { return this.mobile() ? x.mobile : x.desktop; }
  categoriesFor(ids: string[]) { return this.categories().filter(c => !ids.length || ids.includes(c.id)).slice(0,12); }
  productsFor(ids: string[]) { return ids.flatMap(id => this.products().filter(p => p.id === id)); }
  secure(url: string) {
    if (!/^https:\/\/[^\s\\]+$/i.test(url)) return false;
    try { const value = new URL(url); return value.protocol === 'https:' && !value.username && !value.password; } catch { return false; }
  }
  image(url: string) { return this.secure(url) || /^\/uploads\/[a-z0-9_.\/-]+$/i.test(url) && !url.includes('..'); }
  internal(url: string): string[] | null {
    const store = ['/stores', this.slug()];
    if (url === '/' || url === '/products' || url === '/categories') return store;
    if (url === '/cart' || url === '/search') return [...store, url.slice(1)];
    const match = /^\/(products|pages)\/([a-z0-9-]{1,160})$/.exec(url);
    return match ? [...store, match[1], match[2]] : null;
  }
}
