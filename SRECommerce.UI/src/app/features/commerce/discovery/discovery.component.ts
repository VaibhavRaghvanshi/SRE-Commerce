import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom, timeout } from 'rxjs';
import { CommerceCatalogService } from '../../../core/services/commerce-catalog.service';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
import { CommerceLookups } from '../../../core/models/commerce.models';

interface Hit { productId: string; name: string; slug: string; categoryId: string; brandId: string | null; revision: number; }
interface Result { items: Hit[]; page: number; more: boolean; pageCategories: Record<string, number>; pageAttributes: Record<string, string[]>; }
export interface Review { id: string; productId: string; rating: number; body: string; state: number; version: string; updatedAt: string; }

@Component({
  selector: 'app-commerce-discovery', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './discovery.component.html',
  styles: [`:host{display:block;min-height:100vh;background:#f7f8f4;color:#193027}main{max-width:1050px;margin:auto;padding:2rem 1rem}
  article{background:white;border:1px solid #ccd9ce;border-radius:10px;padding:1rem;margin-block:1rem} form{display:flex;align-items:end;gap:1rem;flex-wrap:wrap}
  input,select{display:block;padding:.45rem;border:1px solid #829889;border-radius:5px} button{margin:.35rem .35rem .35rem 0;background:#21563e;color:white;padding:.55rem .8rem;border:0;border-radius:5px}
  button:disabled{opacity:.5} ul{padding-left:1.4rem} li{padding:.4rem 0} [role=alert]{color:#942012}`]
})
export class DiscoveryComponent {
  private readonly http = inject(HttpClient);
  private readonly catalog = inject(CommerceCatalogService);
  private readonly session = inject(CommerceSessionService);
  private readonly route = inject(ActivatedRoute);
  readonly slug = this.route.snapshot.paramMap.get('slug');
  readonly merchant = !this.slug;
  readonly tenant = signal(this.route.snapshot.paramMap.get('tenant') || '');
  readonly name = signal('');
  readonly busy = signal(false);
  readonly error = signal('');
  readonly message = signal('');
  readonly results = signal<Result | null>(null);
  readonly lookups = signal<CommerceLookups | null>(null);
  readonly reviews = signal<Review[]>([]);
  readonly suggestions = signal<Hit[]>([]);
  readonly maxIndexPage = signal(0);
  q = ''; category = ''; brand = ''; sort = 'relevance'; attribute = ''; value = ''; reason = ''; supportReason = '';
  indexPage = 1; reviewPage = 1; reviewState = 0;
  get support() { return !!this.session.access()?.isPlatformAdmin; }
  get manager() {
    const a = this.session.access();
    return !!this.session.token && !!a && (a.isPlatformAdmin || a.selectedTenantId === this.tenant() && ['Owner', 'Manager'].includes(a.tenantRole || ''));
  }
  constructor() { if (typeof window !== 'undefined') void this.initialize(); }
  private async initialize() {
    if (!this.slug) { if (this.manager && !this.support) await this.moderation(); return; }
    try {
      const store = await firstValueFrom(this.catalog.tenant(this.slug).pipe(timeout(10000)));
      if (!store.modules.includes('discovery')) { this.error.set('Advanced search is not enabled for this store.'); return; }
      this.tenant.set(store.tenantId); this.name.set(store.displayName);
      await this.search(1);
      this.lookups.set(await firstValueFrom(this.catalog.lookups(store.tenantId).pipe(timeout(10000))));
    } catch { this.error.set('Some store search features could not be loaded.'); }
  }
  private endpoint(publicRead: boolean) { return '/commerce/discovery/' + (publicRead ? 'public/' : '') + 'tenants/' + encodeURIComponent(this.tenant()) + '/discovery/'; }
  private headers(): Record<string, string> { return this.support ? { 'X-Support-Reason': this.supportReason.trim() } : {}; }
  private async run(work: () => Promise<void>) {
    if (!this.tenant() || this.busy()) return;
    this.busy.set(true); this.error.set(''); this.message.set('');
    try { await work(); }
    catch (e) { this.error.set(e instanceof HttpErrorResponse && e.status === 409 ? 'This review changed. Refresh before trying again.' : 'Discovery could not complete this request. Check access or return to the store catalog.'); }
    finally { this.busy.set(false); }
  }
  async signIn() { try { await this.session.signIn(this.tenant(), 'discovery'); } catch { this.error.set('Sign-in could not be started.'); } }
  search(page: number) { return this.run(async () => {
    this.suggestions.set([]);
    let params = new HttpParams().set('sort', this.sort).set('page', page);
    for (const [key, val] of Object.entries({ q: this.q.trim(), category: this.category, brand: this.brand, attribute: this.attribute && this.value ? this.attribute : '', value: this.attribute ? this.value : '' }))
      if (val) params = params.set(key, val);
    this.results.set(await firstValueFrom(this.http.get<Result>(this.endpoint(true) + 'search', { params }).pipe(timeout(10000))));
  }); }
  suggest() { return this.run(async () => {
    if (!this.q.trim()) return;
    this.suggestions.set(await firstValueFrom(this.http.get<Hit[]>(this.endpoint(true) + 'suggestions', { params: { q: this.q.trim() } }).pipe(timeout(10000))));
  }); }
  reindex() { return this.run(async () => {
    if (!this.manager || this.support && !this.supportReason.trim()) return;
    const result = await firstValueFrom(this.http.post<{ page: number; pageSize: number; indexed: number; total: number }>(this.endpoint(false) + 'index/pages/' + this.indexPage, null, { headers: this.headers() }).pipe(timeout(10000)));
    const pages = Math.ceil(result.total / result.pageSize);
    this.maxIndexPage.set(pages); this.message.set(`Page ${result.page} of ${Math.max(1, pages)} indexed: ${result.indexed} products.`);
    if (this.indexPage < pages) this.indexPage++;
  }); }
  moderation() { return this.run(() => this.loadReviews()); }
  private async loadReviews() {
    if (!this.manager || this.support && !this.supportReason.trim()) return;
    this.reviews.set(await firstValueFrom(this.http.get<Review[]>(this.endpoint(false) + 'moderation', { headers: this.headers(), params: { state: this.reviewState, page: this.reviewPage } }).pipe(timeout(10000))));
  }
  decide(review: Review, state: number) { return this.run(async () => {
    if (!this.manager || !this.reason.trim() || this.support && !this.supportReason.trim()) return;
    await firstValueFrom(this.http.put(this.endpoint(false) + 'moderation/' + review.id, { state, reason: this.reason.trim(), version: review.version }, { headers: this.headers() }).pipe(timeout(10000)));
    this.reason = ''; await this.loadReviews(); this.message.set('Review decision saved.');
  }); }
}
