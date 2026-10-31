import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CommercePricingService } from './commerce-pricing.service';
describe('Authoritative public quote requests',()=>{
  beforeEach(()=>TestBed.configureTestingModule({providers:[provideZonelessChangeDetection(),provideHttpClient(),provideHttpClientTesting()]}));
  afterEach(()=>TestBed.inject(HttpTestingController).verify());
  it('sends SKU quantity and coupon without trusting a client price or discount',()=>{TestBed.inject(CommercePricingService).quote('tenant-a','variant-a',3,' SAVE10 ').subscribe();const request=TestBed.inject(HttpTestingController).expectOne('/commerce/pricing/public/tenants/tenant-a/pricing/quotes');expect(request.request.method).toBe('POST');expect(request.request.body.items).toEqual([{variantId:'variant-a',quantity:3}]);expect(request.request.body.couponCode).toBe('SAVE10');expect(request.request.body.price).toBeUndefined();expect(request.request.body.discount).toBeUndefined();expect(request.request.body.id).toMatch(/^[a-f0-9-]{36}$/);request.flush({});});
  it('issues a fresh preview identity when the customer asks for a new quote',()=>{const api=TestBed.inject(CommercePricingService);api.quote('tenant','variant',1,'').subscribe();const first=TestBed.inject(HttpTestingController).expectOne('/commerce/pricing/public/tenants/tenant/pricing/quotes');const id=first.request.body.id;first.flush({});api.quote('tenant','variant',2,'').subscribe();const second=TestBed.inject(HttpTestingController).expectOne('/commerce/pricing/public/tenants/tenant/pricing/quotes');expect(second.request.body.id).not.toBe(id);expect(second.request.body.couponCode).toBeNull();second.flush({});});
});
