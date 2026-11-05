import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CommerceCatalogService } from './commerce-catalog.service';
import { CommercePricingService } from './commerce-pricing.service';
import { MerchantInventoryService } from './merchant-inventory.service';
import { CommerceSessionService } from './commerce-session.service';
import { commerceAuthInterceptor } from '../interceptors/commerce-auth.interceptor';

describe('Buyer storefront audience routing', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection(),
    provideHttpClient(withInterceptors([commerceAuthInterceptor])), provideHttpClientTesting(),
    { provide: CommerceSessionService, useValue: { token: 'ephemeral-test-bearer', access: () => ({ selectedTenantId: 'tenant-a', tenantRole: 'B2BBuyer' }) } }] }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  function shopping(tenant: string) {
    TestBed.inject(CommerceCatalogService).product(tenant, 'wholesale-item').subscribe();
    TestBed.inject(CommercePricingService).quote(tenant, 'variant-a', 10, '').subscribe();
    TestBed.inject(MerchantInventoryService).availability(tenant, 'variant-a').subscribe();
  }
  it('sends the selected member catalog, price and stock requests through bearer-protected routes', () => {
    shopping('tenant-a'); const http = TestBed.inject(HttpTestingController);
    for (const url of ['/commerce/catalog/tenants/tenant-a/catalog/buyer/products/wholesale-item', '/commerce/pricing/tenants/tenant-a/pricing/quotes', '/commerce/inventory/tenants/tenant-a/inventory/availability']) {
      const request = http.expectOne(r => r.url === url);
      expect(request.request.headers.get('Authorization')).toBe('Bearer ephemeral-test-bearer'); request.flush({});
    }
  });
  it('uses anonymous public routes when the same session visits another tenant store', () => {
    shopping('tenant-b'); const http = TestBed.inject(HttpTestingController);
    for (const service of ['catalog', 'pricing', 'inventory']) {
      const request = http.expectOne(r => r.url.startsWith('/commerce/' + service + '/public/tenants/tenant-b/'));
      expect(request.request.headers.has('Authorization')).toBeFalse(); request.flush({});
    }
  });
});
