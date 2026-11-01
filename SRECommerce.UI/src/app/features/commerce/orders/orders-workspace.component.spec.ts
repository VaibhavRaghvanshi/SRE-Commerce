import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { OrdersWorkspaceComponent } from './orders-workspace.component';
import { MerchantOrderService } from '../../../core/services/merchant-order.service';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
import { CommerceOrder } from '../../../core/models/commerce-order.models';

describe('Orders workspace authorization and recovery', () => {
  let cancel: jasmine.Spy; let list: jasmine.Spy;
  let actor: { selectedTenantId: string; tenantRole: string; isPlatformAdmin: boolean };
  const order = { id: 'order-1', version: 'version-1', historical: false, status: 1, items: [{ shipmentRequested: 0 }] } as CommerceOrder;
  beforeEach(() => {
    actor = { selectedTenantId: 'tenant-a', tenantRole: 'Owner', isPlatformAdmin: false };
    cancel = jasmine.createSpy('cancel').and.returnValue(throwError(() => ({ status: 503 })));
    list = jasmine.createSpy('list').and.returnValue(of({ items: [], total: 0, page: 1, pageSize: 50 }));
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection(),
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ tenant: 'tenant-a' }) } } },
      { provide: CommerceSessionService, useValue: { token: 'memory-only', access: () => actor } },
      { provide: MerchantOrderService, useValue: { list, cancel } }
    ] });
  });
  it('retries an uncertain cancellation with the same operation and order version', () => {
    const c = TestBed.createComponent(OrdersWorkspaceComponent).componentInstance;
    c.selected.set(order); c.note = 'Buyer requested cancellation'; c.cancel();
    const first = cancel.calls.mostRecent().args;
    c.cancel();
    const retry = cancel.calls.mostRecent().args;
    expect(retry[2]).toBe(first[2]);
    expect(retry[1].version).toBe('version-1');
    expect(c.busy()).toBeFalse();
    expect(c.message()).toBe('');
  });
  it('lets staff read orders but prevents cancellation and policy changes', () => {
    actor.tenantRole = 'Staff';
    const c = TestBed.createComponent(OrdersWorkspaceComponent).componentInstance;
    expect(list).toHaveBeenCalled(); expect(c.canManage).toBeFalse(); expect(c.canConfigure).toBeFalse();
    c.selected.set(order); c.note = 'Staff attempt'; c.cancel();
    expect(cancel).not.toHaveBeenCalled();
  });
  it('requires a support reason before platform access to a business', () => {
    actor.isPlatformAdmin = true;
    const c = TestBed.createComponent(OrdersWorkspaceComponent).componentInstance;
    c.reload(); expect(list).not.toHaveBeenCalled(); expect(c.error()).toContain('support reason');
    c.supportReason = 'Investigating order recovery'; c.reload();
    expect(list.calls.mostRecent().args[3]).toBe('Investigating order recovery');
  });
});
