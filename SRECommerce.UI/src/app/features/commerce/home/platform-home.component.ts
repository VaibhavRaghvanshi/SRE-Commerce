import { Component, DestroyRef, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, map, of, switchMap } from 'rxjs';
import { CommerceCatalogService } from '../../../core/services/commerce-catalog.service';
import { CommerceProduct, CommerceTenant } from '../../../core/models/commerce.models';
import { PlatformHomeHeroComponent } from './platform-home-hero.component';
import { PlatformHomeCollectionComponent } from './platform-home-collection.component';

@Component({ selector: 'app-platform-home', standalone: true, imports: [RouterLink, PlatformHomeHeroComponent, PlatformHomeCollectionComponent],
  templateUrl: './platform-home.component.html', styleUrl: './platform-home.component.scss' })
export class PlatformHomeComponent {
  private readonly api = inject(CommerceCatalogService); private readonly destroy = inject(DestroyRef);
  readonly tenant = signal<CommerceTenant | null>(null); readonly products = signal<CommerceProduct[]>([]); readonly total = signal(0);
  readonly loading = signal(false); readonly error = signal(''); readonly failedImages = signal<string[]>([]);
  readonly demoSlug = 'legacy-demo';
  constructor() { if (typeof window !== 'undefined') this.load(); }
  load() {
    if (this.loading()) return; this.loading.set(true); this.error.set(''); this.failedImages.set([]);
    this.api.tenant(this.demoSlug).pipe(switchMap(tenant => this.api.featured(tenant.tenantId).pipe(map(products => ({ tenant, products })))),
      catchError(() => { this.error.set('The demo collection is temporarily unavailable. You can still open your account or business workspace.'); return of(null); }), takeUntilDestroyed(this.destroy))
      .subscribe(value => { if (value) { this.tenant.set(value.tenant); const pictured=value.products.items.filter(product=>product.media.length>0); this.products.set((pictured.length?pictured:value.products.items).slice(0,6)); this.total.set(value.products.total); } this.loading.set(false); });
  }
  imageFailed(id: string) { this.failedImages.update(rows => rows.includes(id) ? rows : [...rows, id]); }
}
