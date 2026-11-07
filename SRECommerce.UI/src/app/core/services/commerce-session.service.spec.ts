import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { User } from 'oidc-client-ts';
import { CommerceSessionService } from './commerce-session.service';

describe('Verified store enrollment', () => {
  const tenant = '25bdef41-42e9-4d89-8d2b-0b0b295f61d0';
  let session: CommerceSessionService;
  let http: HttpTestingController;
  let redirect: jasmine.Spy;
  let callback: jasmine.Spy;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting()] });
    session = TestBed.inject(CommerceSessionService); http = TestBed.inject(HttpTestingController);
    redirect = jasmine.createSpy('signinRedirect').and.resolveTo();
    callback = jasmine.createSpy('signinRedirectCallback');
    Reflect.set(session, 'manager', { signinRedirect: redirect, signinRedirectCallback: callback });
  });
  afterEach(() => http.verify());
  function user(state: unknown) { return { access_token: 'ephemeral-test-token', expired: false, state } as User; }
  async function loadAccess(state: unknown) {
    callback.and.resolveTo(user(state));
    const pending = session.callback();
    await Promise.resolve();
    http.expectOne('/commerce/identity/me/access').flush({ userId: 'customer', isPlatformAdmin: false, selectedTenantId: null, tenantRole: null });
    await Promise.resolve();
    return { pending };
  }
  it('starts with a global PKCE session when no membership is selected', async () => {
    await session.signInStore(tenant, 'legacy-demo', 'account');
    expect(redirect).toHaveBeenCalledOnceWith({ state: { returnTo: '/stores/legacy-demo/account', joinStore: { tenant, slug: 'legacy-demo' } } });
  });
  it('retains the direct tenant sign-in path for an existing selected member', async () => {
    session.user.set(user({}));
    session.access.set({ userId: 'buyer', isPlatformAdmin: false, selectedTenantId: tenant, tenantRole: 'B2BBuyer' });
    await session.signInShop(tenant, 'legacy-demo');
    expect(redirect).toHaveBeenCalledOnceWith({ extraQueryParams: { tenant_id: tenant }, state: { returnTo: '/stores/legacy-demo' } });
  });
  it('joins using the verified global token then requests fresh tenant-bound claims', async () => {
    const { pending } = await loadAccess({ returnTo: '/stores/legacy-demo/account', joinStore: { tenant, slug: 'legacy-demo' } });
    const request = http.expectOne('/commerce/identity/me/stores/' + tenant + '/membership');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Authorization')).toBe('Bearer ephemeral-test-token');
    expect(request.request.body).toEqual({});
    request.flush({ tenantId: tenant, role: 'Customer', joined: true });
    expect(await pending).toBeNull();
    expect(redirect).toHaveBeenCalledOnceWith({ extraQueryParams: { tenant_id: tenant }, state: { returnTo: '/stores/legacy-demo/account' } });
    expect(session.access()?.tenantRole).toBeNull();
  });
  it('does not continue into a revoked membership or invent a role after rejection', async () => {
    const { pending } = await loadAccess({ returnTo: '/stores/legacy-demo/cart', joinStore: { tenant, slug: 'legacy-demo' } });
    const rejection = expectAsync(pending).toBeRejected();
    http.expectOne('/commerce/identity/me/stores/' + tenant + '/membership').flush({ error: 'MembershipUnavailable' }, { status: 403, statusText: 'Forbidden' });
    await rejection;
    expect(redirect).not.toHaveBeenCalled(); expect(session.access()?.tenantRole).toBeNull();
    expect(session.error()).toContain('store owner');
  });
  it('rejects a mismatched store return path before requesting enrollment', async () => {
    callback.and.resolveTo(user({ returnTo: '/stores/another-store/account', joinStore: { tenant, slug: 'legacy-demo' } }));
    const pending = session.callback(); const rejection = expectAsync(pending).toBeRejected();
    await Promise.resolve();
    http.expectOne('/commerce/identity/me/access').flush({ userId: 'customer', isPlatformAdmin: false, selectedTenantId: null, tenantRole: null });
    await rejection; expect(redirect).not.toHaveBeenCalled();
  });
  it('restores the original product after the tenant-bound callback without enrolling twice', async () => {
    const { pending } = await loadAccess({ returnTo: '/stores/legacy-demo/products/test-product' });
    expect(await pending).toBe('/stores/legacy-demo/products/test-product');
    expect(redirect).not.toHaveBeenCalled();
  });
});
