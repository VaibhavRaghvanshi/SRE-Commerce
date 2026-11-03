import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, forkJoin, of, switchMap, tap } from 'rxjs';
import { CommerceCatalogService } from '../../../core/services/commerce-catalog.service';
import { CommerceLookup, CommerceTenant } from '../../../core/models/commerce.models';
import { StoreAppearanceDirective } from './store-appearance.directive';
import { StorefrontBlocksComponent } from './storefront-blocks.component';

@Component({
  selector: 'app-storefront-page', standalone: true, imports: [CommonModule, RouterLink, StoreAppearanceDirective, StorefrontBlocksComponent],
  template: `
    <main [commerceAppearance]="store()" [commercePage]="page()" #look="commerceAppearance">
      <a [routerLink]="['/stores',slug()]">← Back to {{store()?.displayName || 'store'}}</a>
      <p role="status" *ngIf="loading()">Loading store page…</p><p role="alert" *ngIf="failed()">This store page is unavailable.</p>
      <ng-container *ngIf="store() as tenant"><h1>{{tenant.displayName}}</h1>
        <ng-container *ngIf="look.layout() as document; else catalog">
          <app-storefront-blocks [layout]="document" [slug]="tenant.slug" [tenant]="tenant.tenantId" [mobile]="look.mobile()" [categories]="categories()" />
          <app-storefront-blocks [layout]="document" [slug]="tenant.slug" [tenant]="tenant.tenantId" [mobile]="look.mobile()" [footer]="true" />
        </ng-container>
        <ng-template #catalog><p><a [routerLink]="['/stores',tenant.slug]">Browse published products</a></p></ng-template>
      </ng-container>
    </main>
  `,
  styles: [`main{min-height:100vh;padding:2rem max(1rem,calc((100vw - 1100px)/2));background:var(--color-background,#f7f8f4);color:var(--color-text,#20372d);font:var(--font-size,16px)/var(--line-height,1.5) var(--font-family,system-ui)}a{color:inherit}`]
})
export class StorefrontPageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly catalog = inject(CommerceCatalogService);
  private readonly destroy = inject(DestroyRef);
  readonly slug = signal(''); readonly page = signal('home'); readonly store = signal<CommerceTenant | null>(null);
  readonly categories = signal<CommerceLookup[]>([]); readonly loading = signal(true); readonly failed = signal(false);
  constructor() {
    this.route.paramMap.pipe(tap(params => {
      this.slug.set(params.get('slug') || ''); this.page.set(params.get('page') || 'home'); this.store.set(null); this.loading.set(true); this.failed.set(false);
    }), switchMap(() => this.catalog.tenant(this.slug()).pipe(
      switchMap(store => forkJoin({ store: of(store), categories: this.catalog.lookups(store.tenantId).pipe(catchError(() => of(null))) })),
      catchError(() => of(null)))), takeUntilDestroyed(this.destroy)).subscribe(value => {
        this.loading.set(false); this.failed.set(!value || !value.store.modules.includes('storefront'));
        this.store.set(value?.store || null); this.categories.set(value?.categories?.categories || []);
    });
  }
}
