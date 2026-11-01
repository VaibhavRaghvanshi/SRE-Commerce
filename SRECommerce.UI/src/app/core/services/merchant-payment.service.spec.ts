import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { MerchantPaymentService } from './merchant-payment.service';
describe('Payment settings contract', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting()] }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('sends only editable settings and the concurrency version, preserving support authorization', () => {
    TestBed.inject(MerchantPaymentService).saveConfiguration('tenant-a', { provider: 'Sandbox', sandbox: true, active: true, allowCard: true, allowUPI: true, allowNetBanking: false, allowCOD: false, outcome: 1, version: 'v1' }, 'Investigate checkout').subscribe();
    const r = TestBed.inject(HttpTestingController).expectOne('/commerce/payment/tenants/tenant-a/payments/configuration');
    expect(r.request.body.provider).toBeUndefined(); expect(r.request.body.sandbox).toBeUndefined(); expect(r.request.body.version).toBe('v1');
    expect(r.request.headers.get('X-Support-Reason')).toBe('Investigate checkout'); r.flush({});
  });
});
