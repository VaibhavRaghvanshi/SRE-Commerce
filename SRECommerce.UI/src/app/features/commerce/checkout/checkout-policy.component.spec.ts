import { provideZonelessChangeDetection, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { CommerceCheckoutComponent } from './checkout.component';
import { CommerceCatalogService } from '../../../core/services/commerce-catalog.service';
import { CommerceCartService } from '../../../core/services/commerce-cart.service';
import { CommerceCheckoutService } from '../../../core/services/commerce-checkout.service';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
import { CommerceCustomerService } from '../../../core/services/commerce-customer.service';

describe('Checkout policy before contact entry', () => {
  let api: jasmine.SpyObj<CommerceCheckoutService>, carts: jasmine.SpyObj<CommerceCartService>;
  beforeEach(() => {
    api = jasmine.createSpyObj('checkout', ['active', 'regions', 'methods', 'policy']);
    api.active.and.returnValue(null); api.regions.and.resolveTo({ countries: ['IN'], currency: 'INR' }); api.methods.and.resolveTo({ provider: 'Sandbox', configured: true, methods: ['UPI', 'COD'], sandbox: true });
    carts = jasmine.createSpyObj('carts', ['load', 'prepare']); carts.load.and.resolveTo({ items: [{}] } as never);
    carts.prepare.and.resolveTo({ items: [], quote: { currency: 'INR', calculation: { total: 100, discount: 0, tax: 0 } } } as never);
    TestBed.configureTestingModule({ imports: [CommerceCheckoutComponent], providers: [provideZonelessChangeDetection(), provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ slug: 'demo' }), queryParamMap: convertToParamMap({}) } } },
      { provide: CommerceCatalogService, useValue: { tenant: () => of({ tenantId: 'tenant', slug: 'demo', modules: ['cart', 'orders', 'payments', 'shipping'] }) } },
      { provide: CommerceCheckoutService, useValue: api }, { provide: CommerceCartService, useValue: carts },
      { provide: CommerceSessionService, useValue: { user: signal(null), access: signal(null) } },
      { provide: CommerceCustomerService, useValue: { signedIn: () => false } },
    ] });
  });
  it('keeps an unconfigured store out of the order form and explains that the saved cart is safe', async () => {
    api.policy.and.rejectWith(new HttpErrorResponse({ status: 409, error: { title: 'OrderingNotConfigured' } }));
    const fixture = TestBed.createComponent(CommerceCheckoutComponent); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('This store has not finished checkout setup. Your saved cart is safe.');
    expect(fixture.nativeElement.querySelector('[data-testid=place-order]')).toBeNull(); expect(carts.load).not.toHaveBeenCalled();
  });
  it('offers sign-in before requesting cart preparation when guest checkout is disabled', async () => {
    api.policy.and.resolveTo({ allowGuest: false, allowCOD: true, allowExchanges: false, returnWindowDays: 14, maximumTotal: 1000 });
    const fixture = TestBed.createComponent(CommerceCheckoutComponent); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Sign in to continue checkout'); expect(carts.load).not.toHaveBeenCalled();
  });
  it('filters COD when payment supports it but the order policy disallows it', async () => {
    api.policy.and.resolveTo({ allowGuest: true, allowCOD: false, allowExchanges: false, returnWindowDays: 14, maximumTotal: 1000 });
    const fixture = TestBed.createComponent(CommerceCheckoutComponent); await fixture.whenStable();
    expect(fixture.componentInstance.methods()).toEqual(['UPI']); expect(fixture.componentInstance.form.controls.paymentMethod.value).toBe('UPI');
    expect(fixture.nativeElement.querySelector('option[value=COD]')).toBeNull();
  });
});
