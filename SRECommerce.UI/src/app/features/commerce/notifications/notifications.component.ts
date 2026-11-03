import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom, timeout } from 'rxjs';
import { CommerceCatalogService } from '../../../core/services/commerce-catalog.service';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';

interface Preference { email: boolean; sms: boolean; whatsApp: boolean; version: string | null; }
interface Template { kind: number; channel: number; subject: string; body: string; enabled: boolean; version: string; }
interface Job { eventId: string; resourceId: string; kind: number; channel: number; state: number; attempts: number; createdAt: string; }
interface Attempt { attempt: number; state: number; code: string; recordedAt: string; }

@Component({
  selector: 'app-notifications', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './notifications.component.html',
  styles: [`
    :host{display:block;background:#f5f7fb;color:#162536;min-height:100vh}
    main{max-width:1050px;margin:auto;padding:clamp(1rem,4vw,3rem)} header{margin-bottom:1.5rem}
    h1,h2{margin:.2em 0}.eyebrow{text-transform:uppercase;letter-spacing:.14em;color:#315b79;font-weight:700}
    article{border:1px solid #d4e1ec;border-radius:12px;padding:1rem 1.4rem;background:white;margin-block:1rem}
    label{display:block;margin-block:.6rem} input:not([type=checkbox]),select,textarea{display:block;width:min(100%,32rem);padding:.5rem;border:1px solid #7f98ac;border-radius:6px}
    button{background:#124467;color:white;border:0;border-radius:6px;padding:.55rem .8rem;margin:.35rem .5rem .35rem 0;cursor:pointer}
    button:disabled{opacity:.5;cursor:default} .table-wrap{overflow-x:auto} table{width:100%;border-collapse:collapse}
    th,td{text-align:left;padding:.5rem;border-bottom:1px solid #d4e1ec;white-space:nowrap} [role=alert]{color:#992018}
  `]
})
export class NotificationsComponent {
  private readonly http = inject(HttpClient);
  private readonly session = inject(CommerceSessionService);
  private readonly catalog = inject(CommerceCatalogService);
  private readonly route = inject(ActivatedRoute);
  readonly tenant = signal(this.route.snapshot.paramMap.get('tenant') || '');
  readonly slug = this.route.snapshot.paramMap.get('slug');
  readonly merchant = !this.slug;
  readonly busy = signal(false);
  readonly error = signal('');
  readonly message = signal('');
  readonly templates = signal<Template[]>([]);
  readonly jobs = signal<Job[]>([]);
  readonly attempts = signal<Attempt[] | null>(null);
  readonly preference = signal<Preference | null>(null);
  readonly kinds = ['Order confirmed', 'Payment received', 'Order dispatched', 'Order delivered', 'Order cancelled'];
  readonly channels = ['Email', 'SMS (mock)', 'WhatsApp (mock)'];
  readonly states = ['Queued', 'Sending', 'Sent', 'Suppressed', 'Failed', 'Needs review'];
  readonly allowed = computed(() => {
    const a = this.session.access();
    return !!this.session.token && !!a && (this.merchant && a.isPlatformAdmin ||
      a.selectedTenantId === this.tenant() && (!this.merchant || ['Owner', 'Manager'].includes(a.tenantRole || '')));
  });
  readonly support = computed(() => this.merchant && !!this.session.access()?.isPlatformAdmin);
  readonly tokenExample = '{{orderNumber}}';
  reason = '';
  page = 1;
  draft = { kind: 0, channel: 0, subject: '', body: '', enabled: true, version: null as string | null };

  constructor() { if (typeof window !== 'undefined') void this.initialize(); }
  private async initialize() {
    if (this.slug) {
      try {
        const store = await firstValueFrom(this.catalog.tenant(this.slug).pipe(timeout(10000)));
        if (!store.modules.includes('notifications')) { this.error.set('This store does not have notification preferences enabled.'); return; }
        this.tenant.set(store.tenantId);
      } catch { this.error.set('Store details could not be loaded.'); return; }
    }
    if (this.allowed() && !this.support()) await this.reload();
  }
  private request<T>(method: 'GET' | 'PUT', path: string, body?: unknown) {
    return firstValueFrom(this.http.request<T>(method, '/commerce/notification/tenants/' + encodeURIComponent(this.tenant()) + '/notifications/' + path,
      { body, headers: this.support() ? { 'X-Support-Reason': this.reason.trim() } : {} }).pipe(timeout(10000)));
  }
  private async run(action: () => Promise<void>) {
    if (!this.allowed() || this.busy() || this.support() && !this.reason.trim()) return;
    this.busy.set(true); this.error.set(''); this.message.set('');
    try { await action(); }
    catch (e) { this.error.set(e instanceof HttpErrorResponse && e.status === 409 ? 'These settings changed. Refresh before trying again.' : 'Notification settings could not be loaded or saved. Check your access and try again.'); }
    finally { this.busy.set(false); }
  }
  async signIn() {
    if (!this.tenant()) return;
    try { if (this.slug) await this.session.signInStore(this.tenant(), this.slug, 'notifications'); else await this.session.signIn(this.tenant(), 'notifications'); }
    catch { this.error.set('Sign-in could not be started.'); }
  }
  reload() { return this.run(async () => {
    if (!this.merchant) { this.preference.set(await this.request<Preference>('GET', 'preferences')); return; }
    const [templates, jobs] = await Promise.all([this.request<Template[]>('GET', 'templates'), this.request<Job[]>('GET', 'jobs?page=' + this.page)]);
    this.templates.set(templates); this.jobs.set(jobs); this.attempts.set(null); this.selectTemplate();
  }); }
  selectTemplate() {
    const { kind, channel } = this.draft;
    const existing = this.templates().find(t => t.kind === kind && t.channel === channel);
    this.draft = existing ? { ...existing } : { kind, channel, subject: '', body: '', enabled: true, version: null };
  }
  saveTemplate() { return this.run(async () => {
    const { kind, channel, ...value } = this.draft;
    const saved = await this.request<Template>('PUT', 'templates/' + kind + '/' + channel, value);
    this.templates.update(rows => [...rows.filter(t => t.kind !== kind || t.channel !== channel), saved]);
    this.draft = { ...saved }; this.message.set('Template saved.');
  }); }
  savePreference(p: Preference) { return this.run(async () => {
    this.preference.set(await this.request<Preference>('PUT', 'preferences', p)); this.message.set('Preferences saved.');
  }); }
  history(j: Job) { return this.run(async () => {
    this.attempts.set(await this.request<Attempt[]>('GET', 'jobs/' + j.eventId + '/' + j.channel + '/attempts'));
  }); }
}
