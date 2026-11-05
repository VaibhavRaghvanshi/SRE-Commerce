import { Component, computed, provideZonelessChangeDetection, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CommerceImageDirective } from './commerce-image.directive';
import { CommerceSessionService } from '../services/commerce-session.service';
import { commerceAuthInterceptor } from '../interceptors/commerce-auth.interceptor';

const tenant = '11111111-1111-1111-1111-111111111111', asset = '22222222-2222-2222-2222-222222222222';
const managed = '/commerce/catalog/public/tenants/' + tenant + '/catalog/media/' + asset;
@Component({ standalone: true, imports: [CommerceImageDirective], template: '<img [commerceImage]="source()" [imageTenant]="tenant" alt="Product" />' })
class Host { source = signal(managed); tenant = tenant; }

describe('CommerceImageDirective audience isolation', () => {
  const member = signal(true);
  let http: HttpTestingController;
  beforeEach(() => {
    member.set(true);
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection(), provideHttpClient(withInterceptors([commerceAuthInterceptor])), provideHttpClientTesting(),
      { provide: CommerceSessionService, useValue: { get token() { return member() ? 'ephemeral-test-bearer' : null; }, access: computed(() => member() ? { selectedTenantId: tenant } : null) } }] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  it('loads a managed member image through a scoped bearer request and revokes its memory URL on logout', async () => {
    spyOn(URL, 'createObjectURL').and.returnValue('blob:member-image'); const revoke = spyOn(URL, 'revokeObjectURL');
    const fixture = TestBed.createComponent(Host); fixture.detectChanges();
    const request = http.expectOne('/commerce/catalog/tenants/' + tenant + '/catalog/buyer/media/' + asset);
    expect(request.request.headers.get('Authorization')).toBe('Bearer ephemeral-test-bearer');
    request.flush(new Blob(['synthetic-image'], { type: 'image/png' }));
    expect((fixture.nativeElement.querySelector('img') as HTMLImageElement).getAttribute('src')).toBe('blob:member-image');
    member.set(false); fixture.detectChanges(); await fixture.whenStable();
    expect(revoke).toHaveBeenCalledWith('blob:member-image');
    expect((fixture.nativeElement.querySelector('img') as HTMLImageElement).getAttribute('src')).toBe(managed);
    fixture.destroy();
  });
  it('does not attach a bearer request to a different tenant image URL', () => {
    const fixture = TestBed.createComponent(Host); fixture.componentInstance.source.set(managed.replace(tenant, '33333333-3333-3333-3333-333333333333')); fixture.detectChanges();
    http.expectNone(() => true); fixture.destroy();
  });
  it('renders non-managed image sources without sending them through the authenticated HTTP client', () => {
    const fixture = TestBed.createComponent(Host); fixture.componentInstance.source.set('data:image/png;base64,iVBORw0KGgo='); fixture.detectChanges();
    http.expectNone(() => true); fixture.destroy();
  });
});
