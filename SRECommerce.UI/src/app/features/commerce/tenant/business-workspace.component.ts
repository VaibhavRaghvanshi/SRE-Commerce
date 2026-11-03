import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom, timeout } from 'rxjs';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';

interface Business { id: string; ownerUserId: string; name: string; slug: string; businessTypeCode: string; status: number; version: string; }
interface Settings { displayName: string; currency: string; culture: string; timeZone: string; logoUrl: string | null; }
interface Feature { module: string; configured: boolean; entitled: boolean; enabled: boolean; }
interface Entitlements { active: boolean; planCode: string; endsAt: string; maxLocations: number; maxDomains: number; maxUsers: number; modules: Feature[]; }
interface Location { id: string; name: string; address: string; countryCode: string; isActive: boolean; }
interface Domain { id: string; host: string; verifiedAt: string | null; isPrimary: boolean; isActive: boolean; }
interface Challenge { id: string; recordName: string; token: string; expiresAt: string; version: string; }
interface Membership { tenantId?: string; userId?: string; role: string; isActive: boolean; displayName?: string; }
interface Audit { action: string; createdAt: string; platformOperation: boolean; }
interface Plan { code: string; name: string; maxLocations: number; maxDomains: number; maxUsers: number; modules: string[]; }

@Component({
  selector: 'app-business-workspace', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './business-workspace.component.html',
  styles: [`:host{display:block;background:#f5f7fa;color:#182a39;min-height:100vh}main{max-width:1100px;margin:auto;padding:2rem 1rem}
    article{background:#fff;border:1px solid #ccdbe8;border-radius:10px;padding:1rem;margin-block:1rem}label{display:block;margin:.45rem 0}.fields,nav{display:flex;gap:.8rem;flex-wrap:wrap;align-items:end}
    input:not([type=checkbox]),select{display:block;padding:.5rem;max-width:100%;border:1px solid #849bad;border-radius:5px}
    button{background:#164b72;color:white;padding:.5rem .75rem;margin:.35rem .45rem .35rem 0;border:0;border-radius:5px}button:disabled{opacity:.5}li{margin:.35rem 0}
    code{overflow-wrap:anywhere}[role=alert]{color:#96291e}`]
})
export class BusinessWorkspaceComponent {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  readonly session = inject(CommerceSessionService);
  readonly tenant = this.route.snapshot.paramMap.get('tenant') || '';
  readonly platform = this.route.snapshot.data['platform'] === true;
  readonly current = signal<Business | null>(null);
  readonly businesses = signal<Business[]>([]);
  readonly settings = signal<Settings | null>(null);
  readonly entitlements = signal<Entitlements | null>(null);
  readonly locations = signal<Location[]>([]);
  readonly domains = signal<Domain[]>([]);
  readonly audit = signal<Audit[] | null>(null);
  readonly members = signal<Membership[] | null>(null);
  readonly myMemberships = signal<Membership[]>([]);
  readonly types = signal<{ code: string; name: string }[]>([]);
  readonly plans = signal<Plan[]>([]);
  readonly challenge = signal<Challenge | null>(null);
  readonly busy = signal(false); readonly error = signal(''); readonly message = signal('');
  readonly states = ['Draft', 'Pending approval', 'Active', 'Suspended', 'Archived'];
  registration = { name: '', slug: '', businessTypeCode: 'general-store', customBusinessType: '' };
  location = { name: '', address: '', countryCode: 'IN' };
  host = ''; makePrimary = false; userId = ''; memberRole = 'Staff'; suspendReason = ''; planCode = ''; planDays = 30; page = 1;
  private registrationAttempt: { id: string; body: string } | null = null;
  get canManage() {
    const a = this.session.access(), b = this.current();
    return !!this.session.token && !!a && !!b && (a.isPlatformAdmin || a.selectedTenantId === b.id && ['Owner','Manager'].includes(a.tenantRole || '') ||
      a.accountKind === 'BusinessOwner' && a.userId === b.ownerUserId && (b.status === 0 || b.status === 1));
  }
  get canTeam() { const a = this.session.access(); return !!a && a.selectedTenantId === this.current()?.id && ['Owner','Manager'].includes(a.tenantRole || ''); }
  get canRegister() { return this.session.access()?.accountKind === 'BusinessOwner' && !this.platform; }
  constructor() { if (typeof window !== 'undefined' && this.session.token) void this.reload(); }
  private send<T>(module: 'tenant' | 'identity', method: 'GET' | 'POST' | 'PUT' | 'DELETE', path: string, body?: unknown, headers?: Record<string, string>) {
    return firstValueFrom(this.http.request<T>(method, '/commerce/' + module + '/' + path, { body, headers }).pipe(timeout(12000)));
  }
  private async run(action: () => Promise<void>) {
    if (!this.session.token || this.busy()) return;
    this.busy.set(true); this.error.set(''); this.message.set('');
    try { await action(); } catch (e) {
      const code = e instanceof HttpErrorResponse ? e.error?.title || e.error?.error : null;
      this.error.set(typeof code === 'string' && /^[A-Za-z][A-Za-z0-9]{2,60}$/.test(code) ? 'The request was rejected: ' + code :
        e instanceof HttpErrorResponse && e.status === 409 ? 'The business changed or the plan limit was reached. Reload and try again.' : 'The business request failed. Check the details and your access.');
    } finally { this.busy.set(false); }
  }
  async signIn() {
    try { if (this.tenant && !this.platform) await this.session.signIn(this.tenant, 'settings'); else await this.session.signInPortal(this.platform ? 'platform' : 'businesses'); }
    catch { this.error.set('Sign-in could not be started.'); }
  }
  async switchBusiness(id: string) {
    try { await this.session.signIn(id, 'settings'); } catch { this.error.set('Could not select that business.'); }
  }
  private async details(id: string, preserveChallenge = false) {
    if (!preserveChallenge && this.current()?.id !== id) this.challenge.set(null);
    this.members.set(null); this.audit.set(null);
    const prefix = 'tenants/' + id;
    const [business, settings, entitlement, locations, domains] = await Promise.all([
      this.send<Business>('tenant','GET',prefix), this.send<Settings>('tenant','GET',prefix + '/settings'),
      this.send<Entitlements>('tenant','GET',prefix + '/entitlements'), this.send<Location[]>('tenant','GET',prefix + '/locations'),
      this.send<Domain[]>('tenant','GET',prefix + '/domains')
    ]);
    this.current.set(business); this.settings.set(settings); this.entitlements.set(entitlement); this.locations.set(locations); this.domains.set(domains);
    this.planCode = entitlement.planCode;
    this.businesses.update(rows => rows.map(r => r.id === id ? business : r));
  }
  open(id: string) { return this.run(() => this.details(id)); }
  reload() { return this.run(async () => {
    if (this.platform && !this.session.access()?.isPlatformAdmin) { this.error.set('This workspace requires a platform administrator.'); return; }
    if (this.tenant) { await this.details(this.tenant); return; }
    const [businesses, types, plans, memberships] = await Promise.all([
      this.send<Business[]>('tenant','GET','tenants?page=' + this.page), this.send<{ code: string; name: string }[]>('tenant','GET','business-types'),
      this.send<Plan[]>('tenant','GET','plans'), this.send<Membership[]>('identity','GET','me/memberships')
    ]);
    this.businesses.set(businesses); this.types.set(types); this.plans.set(plans); this.myMemberships.set(memberships);
    if (this.current()) await this.details(this.current()!.id);
  }); }
  register() { return this.run(async () => {
    if (!this.canRegister) return;
    const r = this.registration;
    const body = { name: r.name.trim(), slug: r.slug.trim().toLowerCase(), businessTypeCode: r.businessTypeCode, customBusinessType: r.businessTypeCode === 'custom' ? r.customBusinessType.trim() : null };
    const signature = JSON.stringify(body);
    if (this.registrationAttempt && this.registrationAttempt.body !== signature) { this.error.set('Retry the original registration details to check the saved draft.'); return; }
    this.registrationAttempt ??= { id: crypto.randomUUID(), body: signature };
    let business: Business;
    try { business = await this.send<Business>('tenant','POST','tenants',body,{ 'Idempotency-Key': this.registrationAttempt.id }); }
    catch (e) { if (e instanceof HttpErrorResponse && e.status >= 400 && e.status < 500 && e.status !== 429) this.registrationAttempt = null; throw e; }
    this.registrationAttempt = null; this.businesses.update(rows => [business, ...rows.filter(x => x.id !== business.id)]);
    this.registration = { name: '', slug: '', businessTypeCode: 'general-store', customBusinessType: '' };
    await this.details(business.id); this.message.set('Business draft saved.');
  }); }
  mutate(method: 'POST' | 'PUT', suffix: string, value: object = {}, platform = false) { return this.run(async () => {
    const business = this.current();
    if (!business || !this.canManage || platform && !this.session.access()?.isPlatformAdmin) return;
    await this.send<Business>('tenant',method,(platform ? 'platform/' : '') + 'tenants/' + business.id + '/' + suffix,{ ...value, version: business.version });
    await this.details(business.id, true); this.message.set('Business settings updated.');
  }); }
  setSubscription() {
    const now = Date.now(), duration = Number(this.planDays);
    if (!this.planCode || !Number.isInteger(duration) || duration < 1 || duration > 365) return;
    return this.mutate('PUT','subscription',{ planCode: this.planCode, startsAt: new Date(now - 60000).toISOString(), endsAt: new Date(now + duration * 86400000).toISOString(), status: 1 }, true);
  }
  addDomain() { return this.run(async () => {
    const b = this.current(); if (!b || !this.canManage) return;
    this.challenge.set(await this.send<Challenge>('tenant','POST','tenants/' + b.id + '/domains',{ version: b.version, host: this.host.trim() }));
    await this.details(b.id, true); this.host = '';
  }); }
  async verifyDomain() {
    const challenge = this.challenge(); if (!challenge) return;
    await this.mutate('POST','domains/' + challenge.id + '/verify',{ token: challenge.token, makePrimary: this.makePrimary });
    if (!this.error()) this.challenge.set(null);
  }
  loadAudit() { return this.run(async () => { const b = this.current(); if (b && this.canManage) this.audit.set(await this.send<Audit[]>('tenant','GET','tenants/' + b.id + '/audit')); }); }
  loadTeam() { return this.run(async () => { const b = this.current(); if (b && this.canTeam) this.members.set(await this.send<Membership[]>('identity','GET','tenants/' + b.id + '/memberships')); }); }
  team(deactivate?: string) { return this.run(async () => {
    const b = this.current(), user = deactivate || this.userId.trim();
    if (!b || !this.canTeam || !/^[0-9a-f-]{36}$/i.test(user)) return;
    const path = 'tenants/' + b.id + '/memberships';
    await this.send<void>('identity',deactivate ? 'DELETE' : 'PUT',path + '/' + user,deactivate ? undefined : { userId: user, role: this.memberRole });
    this.members.set(await this.send<Membership[]>('identity','GET',path)); this.message.set('Membership updated.');
  }); }
}
