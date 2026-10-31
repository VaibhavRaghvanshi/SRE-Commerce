import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { timeout } from 'rxjs';
import { CommerceLookups, CommercePage, CommerceProduct, CommerceTenant } from '../models/commerce.models';

@Injectable({ providedIn: 'root' })
export class CommerceCatalogService {
  private readonly http = inject(HttpClient);
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
    return this.http.get<CommercePage<CommerceProduct>>(this.base(tenant) + '/products', { params }).pipe(timeout(15000));
  }
  product(tenant: string, slug: string) {
    return this.http.get<CommerceProduct>(this.base(tenant) + '/products/' + encodeURIComponent(slug)).pipe(timeout(15000));
  }
  private base(tenant: string) {
    return `/commerce/catalog/public/tenants/${encodeURIComponent(tenant)}/catalog`;
  }
}
