import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, catchError, forkJoin, map, merge, of, switchMap, takeUntil, tap } from 'rxjs';
import { CommerceCatalogService } from '../../../core/services/commerce-catalog.service';
import { CommerceCartService } from '../../../core/services/commerce-cart.service';
import { CommerceLookups, CommercePage, CommerceProduct, CommerceTenant } from '../../../core/models/commerce.models';
import { StoreAppearanceDirective } from '../presentation/store-appearance.directive';
import { StorefrontBlocksComponent } from '../presentation/storefront-blocks.component';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
import { CommerceImageDirective } from '../../../core/directives/commerce-image.directive';
import { StoreLanguageService } from '../../../core/services/store-language.service';
import { StoreLanguageComponent } from '../presentation/store-language.component';

@Component({
  selector: 'app-commerce-catalog', standalone: true, imports: [CommonModule, FormsModule, RouterLink, StoreAppearanceDirective, StorefrontBlocksComponent, CommerceImageDirective, StoreLanguageComponent],
  templateUrl: './catalog.component.html', styleUrl: './catalog.component.scss'
})
export class CommerceCatalogComponent {
  readonly locale = inject(StoreLanguageService);
  readonly session = inject(CommerceSessionService);
  private readonly api = inject(CommerceCatalogService);
  private readonly carts = inject(CommerceCartService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);
  private readonly restart = new Subject<string>();
  private readonly routeChanged = new Subject<void>();
  private readonly queries = new Subject<{ page: number; search: string; category: string; brand: string; collection: string }>();
  readonly tenant = signal<CommerceTenant | null>(null);
  readonly lookups = signal<CommerceLookups>({ categories: [], brands: [], collections: [] });
  readonly result = signal<CommercePage<CommerceProduct>>({ items: [], total: 0, page: 1, pageSize: 24 });
  readonly loading = signal(true);
  readonly error = signal('');
  readonly addingProduct = signal<string | null>(null);
  readonly cartMessage = signal('');
  readonly failedImages = signal<string[]>([]);
  imageFailed(id: string) { this.failedImages.update(rows => rows.includes(id) ? rows : [...rows, id]); }
  readonly pages = computed(() => Math.max(1, Math.ceil(this.result().total / 24)));
  search = ''; category = ''; brand = ''; collection = '';

  constructor() {
    let previousAudience: string | undefined;
    effect(() => {
      const actor = this.session.access();
      const audience = this.session.token ? [actor?.userId, actor?.selectedTenantId, actor?.tenantRole].join(':') : 'guest';
      if (previousAudience !== undefined && previousAudience !== audience) this.restart.next(this.route.snapshot.paramMap.get('slug') || '');
      previousAudience = audience;
    });
    this.queries.pipe(
      tap(() => { this.loading.set(true); this.error.set(''); }),
      switchMap(query => this.api.products(this.tenant()!.tenantId, query.page, query.search, query.category, query.brand, query.collection)
        .pipe(takeUntil(this.routeChanged), catchError(() => { this.error.set('Products are temporarily unavailable. Please retry.'); return of(null); }))),
      takeUntilDestroyed(this.destroy)
    ).subscribe(result => { if (result) this.result.set(result); this.loading.set(false); });
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroy)).subscribe(params => {
      const id = params.get('category') || '';
      const selected = /^[0-9a-f-]{36}$/i.test(id) ? id : '';
      if (selected !== this.category) { this.category = selected; if (this.tenant()) this.reload(1); }
    });
    merge(this.route.paramMap.pipe(map(params => params.get('slug') || '')), this.restart).pipe(
      tap(() => { this.routeChanged.next(); this.loading.set(true); this.tenant.set(null); this.error.set(''); this.cartMessage.set(''); this.result.set({ items: [], total: 0, page: 1, pageSize: 24 }); }),
      switchMap(slug => this.api.tenant(slug).pipe(
        switchMap(tenant => {
          if (!tenant.modules.includes('catalog')) throw new Error('Catalog unavailable');
          return forkJoin({ tenant: of(tenant), lookups: this.api.lookups(tenant.tenantId) });
        }),
        catchError(() => { this.error.set('This store is unavailable or its catalog is not enabled.'); this.loading.set(false); return of(null); })
      )), takeUntilDestroyed(this.destroy)
    ).subscribe(value => { if (value) { this.tenant.set(value.tenant); this.lookups.set(value.lookups); this.reload(1); } });
  }
  reload(page = 1) {
    if (!this.tenant()) { this.restart.next(this.route.snapshot.paramMap.get('slug') || ''); return; }
    this.queries.next({ page, search: this.search.trim(), category: this.category, brand: this.brand, collection: this.collection });
  }
  reset() { this.search = ''; this.category = ''; this.brand = ''; this.collection = ''; this.reload(1); }

  async quickAdd(product: CommerceProduct) {
    const tenant = this.tenant();
    const variant = product.variants.length === 1 ? product.variants[0] : null;
    if (!tenant?.modules.includes('cart') || !variant?.id || this.addingProduct()) return;
    this.addingProduct.set(product.id);
    this.cartMessage.set('');
    try {
      await this.carts.add(tenant.tenantId, variant.id, 1);
      if (this.tenant() === tenant) this.cartMessage.set('Added to your saved cart.');
    } catch {
      if (this.tenant() === tenant) this.cartMessage.set('The cart change could not be confirmed. Retry this item or view your cart.');
    } finally {
      this.addingProduct.set(null);
    }
  }

  async signIn() { const tenant = this.tenant(); if (tenant) try { await this.session.signInShop(tenant.tenantId, tenant.slug); } catch { this.error.set('Buyer sign-in could not be started.'); } }
}
