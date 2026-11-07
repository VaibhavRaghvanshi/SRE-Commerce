import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom, timeout } from 'rxjs';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';

interface Profile { id: string; email: string; fullName: string; kind: number; emailConfirmed: boolean; mfaEnabled: boolean; }
interface Session { id: string; createdAt: string; expiresAt: string; revokedAt: string | null; mfaAuthenticated: boolean; }
type PublicAction = 'register' | 'confirmation' | 'confirm-email' | 'forgot-password' | 'reset-password';

@Component({
  selector: 'app-commerce-account', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './commerce-account.component.html',
  styles: [`:host{display:block;background:#f5f7fa;color:#1a3040;min-height:100vh}main{max-width:840px;margin:auto;padding:2rem 1rem}article{background:white;border:1px solid #cbd9e5;border-radius:10px;padding:1rem;margin:1rem 0}
    input,select{display:block;padding:.55rem;max-width:100%;border:1px solid #819bad;border-radius:5px}label{display:block;margin:.5rem 0}
    button{background:#164b72;color:white;border:0;border-radius:5px;padding:.55rem .7rem;margin:.35rem .45rem .35rem 0}button:disabled{opacity:.5}code{overflow-wrap:anywhere}[role=alert]{color:#96291e}`]
})
export class CommerceAccountComponent {
  readonly session = inject(CommerceSessionService);
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  readonly storeSlug = inject(ActivatedRoute).snapshot.queryParamMap.get('store') || '';
  readonly profile = signal<Profile | null>(null); readonly sessions = signal<Session[]>([]);
  readonly busy = signal(false); readonly error = signal(''); readonly message = signal('');
  readonly sharedKey = signal(''); readonly recoveryCodes = signal<string[]>([]); readonly signInAgain = signal(false);
  action: PublicAction = 'register';
  email = ''; fullName = ''; password = ''; newPassword = ''; code = ''; kind = 0; mfaCode = ''; mfaPassword = '';
  constructor() {
    this.destroy.onDestroy(() => this.clear());
    if (typeof window !== 'undefined' && this.session.token) void this.reload();
  }
  private clear() { this.password = ''; this.newPassword = ''; this.code = ''; this.mfaCode = ''; this.mfaPassword = ''; this.sharedKey.set(''); this.recoveryCodes.set([]); }
  async signIn() {
    try {
      if (/^[a-z0-9-]{1,80}$/.test(this.storeSlug)) {
        const store = await firstValueFrom(this.http.get<{tenantId:string;slug:string}>('/commerce/tenant/public/tenants/' + this.storeSlug).pipe(timeout(12000)));
        await this.session.signInStore(store.tenantId, store.slug, 'account');
      } else await this.session.signInPortal('account');
    } catch { this.error.set('Sign-in could not be started. The store may be temporarily unavailable.'); }
  }
  private request<T>(method: 'GET' | 'POST' | 'PUT' | 'DELETE', path: string, body?: unknown) {
    return firstValueFrom(this.http.request<T>(method, '/commerce/identity/' + path, { body }).pipe(timeout(12000)));
  }
  private async run(work: () => Promise<void>) {
    if (this.busy()) return;
    this.busy.set(true); this.error.set(''); this.message.set('');
    try { await work(); }
    catch { this.error.set('The account request could not be completed. Check the details and try again.'); }
    finally { this.busy.set(false); this.password = ''; this.newPassword = ''; this.code = ''; this.mfaCode = ''; this.mfaPassword = ''; }
  }
  submitPublic() { return this.run(async () => {
    const body = { email: this.email.trim(), ...(this.action === 'register' ? { password: this.password, fullName: this.fullName.trim(), kind: this.kind } :
      this.action === 'confirm-email' ? { code: this.code.trim() } : this.action === 'reset-password' ? { code: this.code.trim(), password: this.password } : {}) };
    await this.request('POST', 'accounts/' + this.action, body);
    this.message.set(this.action === 'confirm-email' ? 'Email confirmed. You can sign in.' : this.action === 'reset-password' ? 'Password reset. You can sign in.' :
      'If this account is eligible, a message will arrive in its inbox. For local development, check Mailpit.');
  }); }
  private async load() {
    const [p, sessions] = await Promise.all([this.request<Profile>('GET','me'), this.request<Session[]>('GET','me/sessions')]);
    this.profile.set(p); this.fullName = p.fullName; this.sessions.set(sessions);
  }
  reload() { return this.run(() => this.load()); }
  saveProfile() { return this.run(async () => { await this.request('PUT','me',{ fullName: this.fullName.trim() }); await this.load(); this.message.set('Profile saved.'); }); }
  changePassword() { return this.run(async () => {
    await this.request('POST','me/password',{ currentPassword: this.password, newPassword: this.newPassword });
    this.signInAgain.set(true); this.message.set('Password changed. Sign in again.');
  }); }
  setupMfa() { return this.run(async () => {
    const setup = await this.request<{ sharedKey: string }>('POST','me/mfa/setup',{ password: this.mfaPassword });
    this.sharedKey.set(setup.sharedKey); this.message.set('Enter the key in your authenticator app, then enter its current code.');
  }); }
  enableMfa() { return this.run(async () => {
    const setup = await this.request<{ recoveryCodes: string[] }>('POST','me/mfa/enable',{ code: this.mfaCode.trim() });
    this.sharedKey.set(''); this.recoveryCodes.set(setup.recoveryCodes); this.signInAgain.set(true); this.message.set('MFA enabled. Save these recovery codes privately, then sign in again.');
  }); }
  disableMfa() { return this.run(async () => {
    await this.request('POST','me/mfa/disable',{ code: this.mfaCode.trim() });
    this.signInAgain.set(true); this.message.set('MFA disabled. Sign in again.');
  }); }
  revoke(id: string) { return this.run(async () => {
    await this.request('DELETE','me/sessions/' + encodeURIComponent(id));
    try { await this.load(); this.message.set('Session revoked.'); } catch { this.signInAgain.set(true); this.message.set('Session revoked. Sign in again to continue.'); }
  }); }
  async signOut() { try { await this.session.signOut(); } catch { /* The session service clears its local state even after network failure. */ }
    finally { this.clear(); this.profile.set(null); this.sessions.set([]); this.signInAgain.set(false); this.email = ''; this.fullName = ''; } }
}
