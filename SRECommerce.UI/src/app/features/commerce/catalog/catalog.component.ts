import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, catchError, forkJoin, map, merge, of, switchMap, takeUntil, tap } from 'rxjs';
import { CommerceCatalogService } from '../../../core/services/commerce-catalog.service';
import { CommerceLookups, CommercePage, CommerceProduct, CommerceTenant } from '../../../core/models/commerce.models';

@Component({
  selector: 'app-commerce-catalog', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './catalog.component.html', styleUrl: './catalog.component.scss'
})
export class CommerceCatalogComponent {
  private readonly api = inject(CommerceCatalogService);
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
  readonly pages = computed(() => Math.max(1, Math.ceil(this.result().total / 24)));
  search = ''; category = ''; brand = ''; collection = '';

  constructor() {
    this.queries.pipe(
      tap(() => { this.loading.set(true); this.error.set(''); }),
      switchMap(query => this.api.products(this.tenant()!.tenantId, query.page, query.search, query.category, query.brand, query.collection)
        .pipe(takeUntil(this.routeChanged), catchError(() => { this.error.set('Products are temporarily unavailable. Please retry.'); return of(null); }))),
      takeUntilDestroyed(this.destroy)
    ).subscribe(result => { if (result) this.result.set(result); this.loading.set(false); });
    merge(this.route.paramMap.pipe(map(params => params.get('slug') || '')), this.restart).pipe(
      tap(() => { this.routeChanged.next(); this.loading.set(true); this.tenant.set(null); this.error.set(''); this.result.set({ items: [], total: 0, page: 1, pageSize: 24 }); }),
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
}
