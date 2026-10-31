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
  for (const url of ['/api/products','https://external.example/commerce/catalog/tenants/id','/commerce/catalog/public/tenants/id/catalog/products','/commerce-other/catalog/products']) {
    it('does not attach commerce credentials to '+url,()=>{TestBed.inject(HttpClient).get(url).subscribe();const request=TestBed.inject(HttpTestingController).expectOne(url);expect(request.request.headers.has('Authorization')).toBeFalse();request.flush({});});
  }
});
