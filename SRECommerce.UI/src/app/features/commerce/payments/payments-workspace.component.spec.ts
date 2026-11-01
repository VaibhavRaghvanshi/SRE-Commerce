import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { PaymentsWorkspaceComponent } from './payments-workspace.component';
import { MerchantPaymentService, MerchantPayment } from '../../../core/services/merchant-payment.service';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
describe('Payment sandbox actions', () => {
  let decide: jasmine.Spy; let actor: { selectedTenantId: string; tenantRole: string; isPlatformAdmin: boolean };
  const pending = { id: 'p', orderId: 'o', status: 0, version: 'v1', sandbox: true } as MerchantPayment;
  beforeEach(() => {
    actor = { selectedTenantId: 'tenant-a', tenantRole: 'Owner', isPlatformAdmin: false };
    decide = jasmine.createSpy('decide').and.returnValue(throwError(() => ({ status: 503 })));
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection(),
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ tenant: 'tenant-a' }) } } },
      { provide: CommerceSessionService, useValue: { token: 'memory-only', access: () => actor } },
      { provide: MerchantPaymentService, useValue: { list: () => of({ items: [], total: 0 }), decide, configuration: () => throwError(() => ({ status: 404 })) } }
    ] });
  });
  it('retries a lost sandbox response with the same event identity', () => {
    const c = TestBed.createComponent(PaymentsWorkspaceComponent).componentInstance; c.selected.set(pending); c.decide();
    const first = decide.calls.mostRecent().args; c.decide(); const retry = decide.calls.mostRecent().args;
    expect(retry[2]).toBe(first[2]); expect(retry[1].version).toBe('v1'); expect(c.message()).toBe('');
  });
  it('prevents staff and finalized payments from sending sandbox decisions', () => {
    actor.tenantRole = 'Staff'; const c = TestBed.createComponent(PaymentsWorkspaceComponent).componentInstance;
    c.selected.set(pending); c.decide(); expect(decide).not.toHaveBeenCalled();
    actor.tenantRole = 'Owner'; c.selected.set({ ...pending, status: 2 }); c.decide(); expect(decide).not.toHaveBeenCalled();
  });
  it('offers an explicit unsaved configuration when no settings exist', () => {
    const c = TestBed.createComponent(PaymentsWorkspaceComponent).componentInstance; c.loadConfiguration();
    expect(c.configuration?.version).toBeNull(); expect(c.configuration?.provider).toBe('Sandbox'); expect(c.message()).toBe('');
  });
});
