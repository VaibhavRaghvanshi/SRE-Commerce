import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CommerceSessionService } from '../services/commerce-session.service';
import { commerceAuthInterceptor } from './commerce-auth.interceptor';
describe('Commerce access token boundary', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection(), provideHttpClient(withInterceptors([commerceAuthInterceptor])), provideHttpClientTesting(), {provide: CommerceSessionService, useValue: {token: 'commerce-test-token'}}] }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('adds the commerce token only to an approved same-origin resource', () => {
    TestBed.inject(HttpClient).get('/commerce/catalog/tenants/tenant/catalog/products').subscribe();
    const request=TestBed.inject(HttpTestingController).expectOne('/commerce/catalog/tenants/tenant/catalog/products');expect(request.request.headers.get('Authorization')).toBe('Bearer commerce-test-token');request.flush({});
  });
  it('authenticates inventory mutations through the same gateway boundary',()=>{TestBed.inject(HttpClient).post('/commerce/inventory/tenants/tenant/inventory/stock-changes',{}).subscribe();const request=TestBed.inject(HttpTestingController).expectOne('/commerce/inventory/tenants/tenant/inventory/stock-changes');expect(request.request.headers.get('Authorization')).toBe('Bearer commerce-test-token');request.flush({});});
  it('authenticates pricing maintenance without adding credentials to public quotes',()=>{const client=TestBed.inject(HttpClient);client.get('/commerce/pricing/tenants/tenant/pricing/prices').subscribe();const admin=TestBed.inject(HttpTestingController).expectOne('/commerce/pricing/tenants/tenant/pricing/prices');expect(admin.request.headers.get('Authorization')).toBe('Bearer commerce-test-token');admin.flush({});client.post('/commerce/pricing/public/tenants/tenant/pricing/quotes',{}).subscribe();const publicRequest=TestBed.inject(HttpTestingController).expectOne('/commerce/pricing/public/tenants/tenant/pricing/quotes');expect(publicRequest.request.headers.has('Authorization')).toBeFalse();publicRequest.flush({});});
  it('authenticates private customer carts',()=>{TestBed.inject(HttpClient).get('/commerce/cart/tenants/id/cart/carts/example').subscribe();const request=TestBed.inject(HttpTestingController).expectOne('/commerce/cart/tenants/id/cart/carts/example');expect(request.request.headers.get('Authorization')).toBe('Bearer commerce-test-token');request.flush({});});
  for (const url of ['/api/products','https://external.example/commerce/catalog/tenants/id','/commerce/catalog/public/tenants/id/catalog/products','/commerce/cart/public/tenants/id/cart/carts','/commerce-other/catalog/products']) {
    it('does not attach commerce credentials to '+url,()=>{TestBed.inject(HttpClient).get(url).subscribe();const request=TestBed.inject(HttpTestingController).expectOne(url);expect(request.request.headers.has('Authorization')).toBeFalse();request.flush({});});
  }
});
