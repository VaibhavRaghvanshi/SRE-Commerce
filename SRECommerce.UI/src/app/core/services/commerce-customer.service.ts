import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';
import { CustomerAddress, CustomerAddressDraft, SavedProduct, SavedProductPage } from '../models/commerce-customer.models';
import { CommerceSessionService } from './commerce-session.service';
import { CommerceCatalogService } from './commerce-catalog.service';

@Injectable({ providedIn: 'root' })
export class CommerceCustomerService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(CommerceSessionService);
  private readonly catalog = inject(CommerceCatalogService);
  signedIn(tenant: string) { return !!this.session.token && this.session.access()?.selectedTenantId === tenant; }
  private base(tenant: string) { return '/commerce/cart/tenants/' + encodeURIComponent(tenant) + '/cart/account'; }
  addresses(tenant: string) { return firstValueFrom(this.http.get<CustomerAddress[]>(this.base(tenant) + '/addresses').pipe(timeout(10000))); }
  saveAddress(tenant: string, id: string, input: CustomerAddressDraft) {
    return firstValueFrom(this.http.put<CustomerAddress>(this.base(tenant) + '/addresses/' + encodeURIComponent(id), input).pipe(timeout(10000)));
  }
  archiveAddress(tenant: string, address: CustomerAddress) {
    return firstValueFrom(this.http.post<void>(this.base(tenant) + '/addresses/' + address.id + '/archive', { version: address.version }).pipe(timeout(10000)));
  }
  saved(tenant: string, kind: number, page = 1) {
    return firstValueFrom(this.http.get<SavedProductPage>(this.base(tenant) + '/products/' + kind, { params: { page } }).pipe(timeout(10000)));
  }
  wishlist(tenant: string, product: string, active = true, version: string | null = null) {
    return firstValueFrom(this.http.put<SavedProduct>(this.base(tenant) + '/products/0/' + encodeURIComponent(product), { active, version }).pipe(timeout(10000)));
  }
  private recentKey(tenant: string) { return 'srecommerce.recent-products.' + tenant; }
  private localRecent(tenant: string): string[] {
    if (typeof window === 'undefined') return [];
    try {
      const value: unknown = JSON.parse(window.localStorage.getItem(this.recentKey(tenant)) || '[]');
      return Array.isArray(value) ? value.filter((id): id is string => typeof id === 'string' && /^[a-f0-9-]{36}$/i.test(id)).slice(0, 25) : [];
    } catch { return []; }
  }
  async viewed(tenant: string, product: string) {
    if (this.signedIn(tenant)) {
      await firstValueFrom(this.http.put(this.base(tenant) + '/products/1/' + encodeURIComponent(product), { active: true, version: null }).pipe(timeout(10000)));
    } else if (typeof window !== 'undefined') {
      try { window.localStorage.setItem(this.recentKey(tenant), JSON.stringify([product, ...this.localRecent(tenant).filter(id => id !== product)].slice(0, 25))); } catch { /* Browsing still works when storage is disabled. */ }
    }
  }
  async recent(tenant: string): Promise<SavedProduct[]> {
    if (this.signedIn(tenant)) return (await this.saved(tenant, 1)).items;
    const ids = this.localRecent(tenant); if (!ids.length) return [];
    const products = await firstValueFrom(this.catalog.publicProductsById(tenant, ids).pipe(timeout(10000)));
    const lookup = new Map(products.map(p => [p.id, p]));
    return ids.flatMap(id => { const p = lookup.get(id); return p ? [{ productId: p.id, kind: 1, available: true, name: p.name, slug: p.slug, updatedAt: '', version: '' }] : []; });
  }
  async clearRecent(tenant: string) {
    if (this.signedIn(tenant)) await firstValueFrom(this.http.post<void>(this.base(tenant) + '/recently-viewed/clear', null).pipe(timeout(10000)));
    if (typeof window !== 'undefined') try { window.localStorage.removeItem(this.recentKey(tenant)); } catch { /* No persisted history to clear. */ }
  }
}
