import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CommerceCheckoutService } from './commerce-checkout.service';
import { CommerceCartService } from './commerce-cart.service';
import { CommerceSessionService } from './commerce-session.service';
import { CommercePreparation } from '../models/commerce-cart.models';
import { CommerceShippingOption } from '../models/commerce-checkout.models';

describe('CommerceCheckoutService', () => {
  const tenant = '11111111-1111-1111-1111-111111111111';
  const key = 'srecommerce.orders.v1.' + tenant;
  const base = '/commerce/order/public/tenants/' + tenant + '/orders';
  const contact = { fullName: 'Demo Buyer', email: 'buyer@example.test', phone: null, addressLine1: '1 Demo Street', addressLine2: null, city: 'Pune', countryCode: 'IN', postalCode: '411001' };
  let service: CommerceCheckoutService, http: HttpTestingController, preparation: CommercePreparation, shipping: CommerceShippingOption;
  beforeEach(() => {
    localStorage.removeItem(key);
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting(),
      { provide: CommerceCartService, useValue: { checkoutContext: () => ({ guest: true, cartKey: 'cart-capability' }) } },
      { provide: CommerceSessionService, useValue: { token: null, access: () => null } }] });
    service = TestBed.inject(CommerceCheckoutService); http = TestBed.inject(HttpTestingController);
    preparation = { id: crypto.randomUUID(), userId: null } as CommercePreparation;
    shipping = { id: crypto.randomUUID(), preparationId: preparation.id, expiresAt: new Date(Date.now() + 60000).toISOString() } as CommerceShippingOption;
  });
  afterEach(() => { http.verify(); localStorage.removeItem(key); });
  it('retries an uncertain guest checkout with the same request and keeps contact and Cart proof out of storage', async () => {
    const attempt = service.submit(tenant, preparation, shipping, 'COD', contact);
    const failed = expectAsync(attempt).toBeRejected();
    const first = http.expectOne(base + '/checkout');
    const body = first.request.body;
    const proof = first.request.headers.get('X-Order-Key')!;
    expect(proof).toMatch(/^[A-Za-z0-9_-]{43}$/);
    expect(first.request.headers.get('X-Cart-Key')).toBe('cart-capability');
    expect(Object.keys(body).sort()).toEqual(['contact', 'id', 'paymentMethod', 'preparationId', 'shippingQuoteId']);
    first.flush({}, { status: 503, statusText: 'Unavailable' });
    await failed;
    const stored = localStorage.getItem(key)!;
    expect(stored).not.toContain(contact.email);
    expect(stored).not.toContain('cart-capability');
    const retry = service.submit(tenant, preparation, shipping, 'COD', contact);
    const second = http.expectOne(base + '/checkout');
    expect(second.request.body).toEqual(body);
    expect(second.request.headers.get('X-Order-Key')).toBe(proof);
    second.flush({ id: body.id, state: 2 });
    await retry;
    const status = service.status(tenant, body.id);
    const read = http.expectOne(base + '/checkouts/' + body.id);
    expect(read.request.headers.get('X-Order-Key')).toBe(proof);
    expect(read.request.headers.has('X-Cart-Key')).toBeFalse();
    read.flush({ id: body.id, state: 2 });
    await status;
    expect(service.active(tenant)).toBeNull();
  });
  it('cancels a pending guest checkout with only the order capability', async () => {
    const submit = service.submit(tenant, preparation, shipping, 'COD', contact);
    const start = http.expectOne(base + '/checkout');
    const id = start.request.body.id;
    const proof = start.request.headers.get('X-Order-Key');
    start.flush({ id, state: 1 });
    await submit;
    const cancel = service.cancelPending(tenant, id, 'Customer requested checkout cancellation');
    const request = http.expectOne(base + '/checkouts/' + id + '/cancel');
    expect(request.request.headers.get('X-Order-Key')).toBe(proof);
    expect(request.request.headers.has('X-Cart-Key')).toBeFalse();
    expect(request.request.body).toEqual({ reason: 'Customer requested checkout cancellation' });
    request.flush({ id, state: 3 });
    await cancel;
    expect(service.active(tenant)?.state).toBe(3);
  });
});
