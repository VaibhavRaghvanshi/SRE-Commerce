import { Injectable, inject, signal } from '@angular/core';
import { HttpBackend, HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { InMemoryWebStorage, User, UserManager, WebStorageStateStore } from 'oidc-client-ts';

export interface CommerceAccess { userId: string; isPlatformAdmin: boolean; selectedTenantId: string | null; tenantRole: string | null; }
@Injectable({ providedIn: 'root' })
export class CommerceSessionService {
  // This client bypasses interceptors to avoid a session/interceptor injection cycle.
  private readonly http = new HttpClient(inject(HttpBackend));
  private manager: UserManager | null = null;
  readonly user = signal<User | null>(null);
  readonly access = signal<CommerceAccess | null>(null);
  readonly error = signal('');
  get token() { const user = this.user(); return user && !user.expired ? user.access_token : null; }
  private client() {
    if (typeof window === 'undefined') throw new Error('Sign-in is available in the browser.');
    if (!this.manager) {
      this.manager = new UserManager({
        authority: 'http://localhost:5181/', client_id: 'srecommerce-angular', redirect_uri: window.location.origin + '/commerce/callback',
        response_type: 'code', scope: 'openid profile email offline_access identity_api tenant_api commerce_api',
        userStore: new WebStorageStateStore({ store: new InMemoryWebStorage() }),
        stateStore: new WebStorageStateStore({ store: window.sessionStorage, prefix: 'srecommerce.pkce.' }),
        automaticSilentRenew: true, monitorSession: false, loadUserInfo: false, staleStateAgeInSeconds: 600
      });
      this.manager.events.addUserLoaded(user => this.user.set(user));
      this.manager.events.addUserUnloaded(() => { this.user.set(null); this.access.set(null); });
      this.manager.events.addSilentRenewError(() => { this.error.set('Your session expired. Please sign in again.'); this.user.set(null); this.access.set(null); });
    }
    return this.manager;
  }
  async signIn(tenant: string, area:'catalog'|'inventory'|'pricing'='catalog') {
    if (!/^[a-f0-9-]{36}$/i.test(tenant)) throw new Error('Choose a valid business.');
    this.error.set('');
    await this.client().signinRedirect({ extraQueryParams: { tenant_id: tenant }, state: { returnTo: `/workspace/${tenant}/${area}` } });
  }
  async signInStore(tenant: string, slug: string) {
    if (!/^[a-f0-9-]{36}$/i.test(tenant) || !/^[a-z0-9-]{1,80}$/.test(slug)) throw new Error('Choose a valid store.');
    this.error.set('');
    await this.client().signinRedirect({ extraQueryParams: { tenant_id: tenant }, state: { returnTo: '/stores/' + slug + '/cart' } });
  }
  async callback() {
    const user = await this.client().signinRedirectCallback(); this.user.set(user);
    window.history.replaceState({}, document.title, '/commerce/callback');
    const access = await firstValueFrom(this.http.get<CommerceAccess>('/commerce/identity/me/access', { headers: { Authorization: 'Bearer ' + user.access_token } }));
    this.access.set(access);
    const state = user.state as { returnTo?: string } | undefined;
    const path = state?.returnTo || '';
    return /^\/workspace\/[a-f0-9-]{36}\/(catalog|inventory|pricing)$/i.test(path) || /^\/stores\/[a-z0-9-]{1,80}\/cart$/.test(path) ? path : '/';
  }
  async signOut() {
    const token = this.token;
    try { if (token) await firstValueFrom(this.http.post('/commerce/identity/logout', null, { headers: { Authorization: 'Bearer ' + token } })); }
    finally { this.client().stopSilentRenew(); await this.client().removeUser(); this.user.set(null); this.access.set(null); }
  }
}
