import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { timeout } from 'rxjs';
import { CommerceLookups, CommercePage, CommerceProduct, CommerceTenant } from '../models/commerce.models';
import { CommerceSessionService } from './commerce-session.service';

@Injectable({ providedIn: 'root' })
export class CommerceCatalogService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(CommerceSessionService);
  member(tenant: string) { return !!this.session.token && this.session.access()?.selectedTenantId === tenant; }
  private shoppingBase(tenant: string) { return this.member(tenant) ? `/commerce/catalog/tenants/${encodeURIComponent(tenant)}/catalog/buyer` : this.base(tenant); }
  tenant(slug: string) {
    return this.http.get<CommerceTenant>(`/commerce/tenant/public/tenants/${encodeURIComponent(slug)}`).pipe(timeout(15000));
  }
  lookups(tenant: string) {
    return this.http.get<CommerceLookups>(this.base(tenant) + '/lookups').pipe(timeout(15000));
  }
  products(tenant: string, page: number, search: string, category: string, brand: string, collection: string) {
    let params = new HttpParams().set('page', page).set('pageSize', 24);
    for (const [key, value] of Object.entries({ search, categoryId: category, brandId: brand, collection })) {
      if (value) params = params.set(key, value);
    }
    return this.http.get<CommercePage<CommerceProduct>>(this.shoppingBase(tenant) + '/products', { params }).pipe(timeout(15000));
  }
  product(tenant: string, slug: string) {
    return this.http.get<CommerceProduct>(this.shoppingBase(tenant) + '/products/' + encodeURIComponent(slug)).pipe(timeout(15000));
  }
  publicProductsById(tenant: string, ids: string[]) {
    return this.http.get<CommerceProduct[]>(this.base(tenant) + '/published-products', { params: { ids } });
  }
  shoppingProductsById(tenant: string, ids: string[]) {
    return this.http.get<CommerceProduct[]>(this.shoppingBase(tenant) + '/published-products', { params: { ids } }).pipe(timeout(15000));
  }
  private base(tenant: string) {
    return `/commerce/catalog/public/tenants/${encodeURIComponent(tenant)}/catalog`;
  }
}
