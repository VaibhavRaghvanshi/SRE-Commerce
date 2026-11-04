import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CommerceCustomerService } from './commerce-customer.service';
import { CommerceSessionService } from './commerce-session.service';

describe('CommerceCustomerService privacy and live catalog reconciliation', () => {
  const tenant = '11111111-1111-1111-1111-111111111111', product = '22222222-2222-2222-2222-222222222222';
  const key = 'srecommerce.recent-products.' + tenant;
  let api: CommerceCustomerService, http: HttpTestingController, signedIn: boolean;
  beforeEach(() => {
    signedIn = false; localStorage.removeItem(key);
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting(),
      { provide: CommerceSessionService, useValue: { get token() { return signedIn ? 'memory-only-token' : null; }, access: () => signedIn ? { selectedTenantId: tenant } : null } }] });
    api = TestBed.inject(CommerceCustomerService); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { http.verify(); localStorage.removeItem(key); });
  it('keeps guest history deduplicated and stores only product IDs for this store', async () => {
    await api.viewed(tenant, product); await api.viewed(tenant, product);
    expect(JSON.parse(localStorage.getItem(key)!)).toEqual([product]);
    const result = api.recent(tenant);
    const request = http.expectOne(r => r.url === '/commerce/catalog/public/tenants/' + tenant + '/catalog/published-products');
    expect(request.request.params.getAll('ids')).toEqual([product]);
    request.flush([{ id: product, name: 'Current product', slug: 'current-product' }]);
    expect((await result)[0].name).toBe('Current product');
  });
  it('does not show unavailable cached products and removes browser history when cleared', async () => {
    await api.viewed(tenant, product); const result = api.recent(tenant);
    http.expectOne(r => r.url.endsWith('/published-products')).flush([]);
    expect(await result).toEqual([]); await api.clearRecent(tenant); expect(localStorage.getItem(key)).toBeNull();
  });
  it('keeps signed-in browsing history on the server and never stores a bearer token', async () => {
    signedIn = true; const view = api.viewed(tenant, product);
    http.expectOne('/commerce/cart/tenants/' + tenant + '/cart/account/products/1/' + product).flush({}); await view;
    expect(localStorage.getItem(key)).toBeNull();
    const recent = api.recent(tenant); http.expectOne(r => r.url.endsWith('/products/1')).flush({ items: [], page: 1, hasMore: false });
    expect(await recent).toEqual([]);
  });
  it('sends the saved row version when removing a wishlist entry', async () => {
    signedIn = true; const result = api.wishlist(tenant, product, false, 'row-version');
    const request = http.expectOne('/commerce/cart/tenants/' + tenant + '/cart/account/products/0/' + product);
    expect(request.request.body).toEqual({ active: false, version: 'row-version' }); request.flush({ productId: product }); await result;
  });
  it('never persists delivery contact details in browser storage', async () => {
    signedIn = true;
    const address = { label: 'Home', fullName: 'Sample Customer', email: 'customer@example.test', phone: null,
      addressLine1: '1 Test Street', addressLine2: null, city: 'Pune', countryCode: 'IN', postalCode: '411001', version: null };
    const save = api.saveAddress(tenant, product, address);
    const request = http.expectOne('/commerce/cart/tenants/' + tenant + '/cart/account/addresses/' + product);
    request.flush({ ...address, id: product, version: 'saved' }); await save;
    expect(localStorage.getItem(key)).toBeNull(); expect(sessionStorage.getItem(key)).toBeNull();
  });
});
