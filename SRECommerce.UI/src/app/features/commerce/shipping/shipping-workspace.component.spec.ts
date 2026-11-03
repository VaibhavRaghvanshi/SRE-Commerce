import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ShippingWorkspaceComponent } from './shipping-workspace.component';
import { MerchantShippingService, ShippingShipment } from '../../../core/services/merchant-shipping.service';
import { MerchantInventoryService } from '../../../core/services/merchant-inventory.service';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
describe('Shipping workflow safety', () => {
  let state: jasmine.Spy, schedule: jasmine.Spy; let actor: { selectedTenantId: string; tenantRole: string; isPlatformAdmin: boolean };
  beforeEach(() => {
    actor = { selectedTenantId: 'tenant-a', tenantRole: 'Owner', isPlatformAdmin: false };
    state = jasmine.createSpy('state').and.returnValue(throwError(() => ({ status: 503 }))); schedule = jasmine.createSpy('schedule').and.returnValue(throwError(() => ({ status: 503 })));
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection(), { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ tenant: 'tenant-a' }) } } },
      { provide: CommerceSessionService, useValue: { token: 'memory-only', access: () => actor } }, { provide: MerchantInventoryService, useValue: {} },
      { provide: MerchantShippingService, useValue: { state, schedule, lookups: () => of({ zones: [], methods: [], currency: 'INR' }), shipments: () => of({ items: [], total: 0 }), pickups: () => of([]) } }
    ] });
  });
  it('keeps the same shipment operation when a response is uncertain', () => {
    const c = TestBed.createComponent(ShippingWorkspaceComponent).componentInstance;
    c.history.set({ shipment: { id: 'parcel', status: 1, version: 'v1' } as ShippingShipment, contact: {} as any, events: [], callbacks: [] }); c.note = 'Handed to local delivery'; c.move(2);
    const first = state.calls.mostRecent().args[2]; c.move(2); expect(state.calls.mostRecent().args[2]).toEqual(first); expect(c.busy()).toBeFalse();
  });
  it('does not let staff change shipment state', () => {
    actor.tenantRole = 'Staff'; const c = TestBed.createComponent(ShippingWorkspaceComponent).componentInstance;
    c.history.set({ shipment: { id: 'parcel', status: 1, version: 'v1' } as ShippingShipment, contact: {} as any, events: [], callbacks: [] }); c.note = 'Not authorized'; c.move(2); expect(state).not.toHaveBeenCalled();
  });
  it('preserves pickup identity and rejects invalid date input before starting a request', () => {
    const c = TestBed.createComponent(ShippingWorkspaceComponent).componentInstance; c.returnId = 'approved-return'; c.pickupWarehouse = 'warehouse'; c.pickupFrom = 'invalid'; c.pickupTo = 'invalid'; c.schedule(); expect(schedule).not.toHaveBeenCalled(); expect(c.busy()).toBeFalse();
    c.pickupFrom = '2026-12-01T10:00'; c.pickupTo = '2026-12-01T11:00'; c.schedule(); const first = schedule.calls.mostRecent().args[1]; c.schedule(); expect(schedule.calls.mostRecent().args[1]).toEqual(first);
  });
});
