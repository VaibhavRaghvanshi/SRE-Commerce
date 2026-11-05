import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PlatformOverviewComponent } from './platform-overview.component';

describe('Platform overview results and uncertain writes', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  async function view() {
    const fixture = TestBed.createComponent(PlatformOverviewComponent);
    http.expectOne('/commerce/tenant/platform/overview').flush({ sampledAt: '2026-11-05T10:00:00Z', totalTenants: 12, effectiveSubscriptions: 3, expiringNextSevenDays: 2, activePlans: 4,
      tenantStates: [{ status: 2, count: 3 }], businessTypes: [], configuredModules: [], registrations: [] });
    await Promise.resolve(); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges(); return fixture;
  }
  it('renders the server aggregate sample without presenting counts as paid revenue', async () => {
    const fixture = await view();
    const values = Array.from(fixture.nativeElement.querySelectorAll('.metrics strong') as NodeListOf<HTMLElement>).map(element => element.textContent?.trim());
    expect(values).toEqual(['12', '3', '2', '4']);
    expect(fixture.nativeElement.textContent).toContain('do not represent sales revenue or paid invoices'); fixture.destroy();
  });
  it('keeps unconfigured/unavailable services visible and labels stale samples after rate limiting', async () => {
    const fixture = await view(), component = fixture.componentInstance;
    const check = component.checkHealth();
    http.expectOne('/commerce/tenant/platform/service-health').flush({ sampledAt: '2026-11-05T10:00:00Z', services: [{ service: 'Catalog', status: 'NotConfigured', elapsedMilliseconds: null }, { service: 'Order', status: 'Unavailable', elapsedMilliseconds: 20 }] });
    await check; fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('NotConfigured'); expect(fixture.nativeElement.textContent).toContain('Unavailable');
    const retry = component.checkHealth(); http.expectOne('/commerce/tenant/platform/service-health').flush(null, { status: 429, statusText: 'Too Many Requests' }); await retry;
    expect(component.health()?.services.length).toBe(2); expect(component.healthError()).toContain('Wait a minute'); fixture.destroy();
  });
  it('preserves the plan code and does not announce creation when the write result is uncertain', async () => {
    const fixture = await view(), component = fixture.componentInstance; const created = spyOn(component.plansChanged, 'emit');
    component.plan.code = 'verified-plan'; component.plan.name = 'Verified plan';
    const save = component.createPlan(), request = http.expectOne('/commerce/tenant/platform/plans');
    expect(request.request.body.code).toBe('verified-plan'); expect(request.request.body.modules).toEqual(['catalog', 'storefront']);
    request.flush(null, { status: 503, statusText: 'Service Unavailable' }); await save;
    expect(component.plan.code).toBe('verified-plan'); expect(component.planMessage()).toContain('could not be confirmed'); expect(created).not.toHaveBeenCalled(); fixture.destroy();
  });
});
