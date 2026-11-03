import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, firstValueFrom, forkJoin, of, timeout } from 'rxjs';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
import type { Review } from './discovery.component';

interface PublicReview { rating: number; body: string; verifiedPurchase: boolean; updatedAt: string; }

@Component({
  selector: 'app-commerce-product-reviews', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <article><h2>Verified purchase reviews</h2><p role="alert" *ngIf="error()">{{error()}}</p>
    <p *ngIf="available() && !reviews().length">No approved reviews yet.</p>
    <ul><li *ngFor="let review of reviews()"><strong>{{review.rating}} / 5</strong> · Verified purchase · {{review.updatedAt | date:'mediumDate'}}<p>{{review.body}}</p></li></ul>
    <form *ngIf="canReview()" #editor="ngForm" (ngSubmit)="save()">
      <p>Review a product after receiving it. All changes go through moderation.</p>
      <label>Rating <select name="rating" [(ngModel)]="rating"><option *ngFor="let n of [5,4,3,2,1]" [ngValue]="n">{{n}} / 5</option></select></label>
      <label>Review <textarea name="body" [(ngModel)]="body" maxlength="1500" rows="4" required></textarea></label>
      <button [disabled]="!ready() || busy() || editor.invalid || !body.trim()">Submit for review</button>
      <p role="status" *ngIf="mine()">Status: {{states[mine()!.state]}}</p>
    </form>
    <button *ngIf="!canReview()" type="button" (click)="signIn()">Sign in to review your purchase</button>
    </article>
    <article *ngIf="related().length"><h2>Related products</h2><ul><li *ngFor="let item of related()"><a [routerLink]="['/stores', store(), 'products', item.slug]">{{item.name}}</a></li></ul></article>
  `,
  styles: [`article{margin:1.25rem 0;padding:1rem;border:1px solid #cbd9ce;border-radius:10px;background:white}li p{white-space:pre-line}
    label{display:block;margin:.5rem 0}textarea{display:block;width:min(100%,36rem)}button{background:#24533c;color:#fff;border:0;border-radius:5px;padding:.5rem .8rem}
    button:disabled{opacity:.5}[role=alert]{color:#90241d}`]
})
export class ProductReviewsComponent {
  private readonly http = inject(HttpClient);
  private readonly session = inject(CommerceSessionService);
  readonly tenant = input.required<string>(); readonly store = input.required<string>(); readonly product = input.required<string>(); readonly productSlug = input.required<string>();
  readonly reviews = signal<PublicReview[]>([]); readonly related = signal<{ slug: string; name: string }[]>([]);
  readonly mine = signal<Review | null>(null); readonly ready = signal(false); readonly available = signal(false); readonly busy = signal(false); readonly error = signal('');
  readonly states = ['Pending moderation', 'Approved', 'Rejected'];
  readonly canReview = computed(() => !!this.session.token && this.session.access()?.selectedTenantId === this.tenant() && !this.session.access()?.isPlatformAdmin);
  rating = 5; body = '';
  constructor() {
    effect(onCleanup => {
      const publicPath = this.path(true), ownPath = this.path(false), signed = this.canReview();
      this.ready.set(false); this.available.set(false); this.reviews.set([]); this.related.set([]); this.mine.set(null); this.rating = 5; this.body = ''; this.error.set('');
      if (typeof window === 'undefined') return;
      const sub = forkJoin({
        reviews: this.http.get<PublicReview[]>(publicPath + '/reviews').pipe(timeout(10000), catchError(() => of(null))),
        related: this.http.get<{ slug: string; name: string }[]>(publicPath + '/related').pipe(timeout(10000), catchError(() => of([]))),
        mine: signed ? this.http.get<Review | null>(ownPath + '/my-review').pipe(timeout(10000), catchError(() => of(undefined))) : of(null)
      }).subscribe(r => {
        this.available.set(r.reviews !== null); this.reviews.set(r.reviews || []); this.related.set(r.related);
        if (r.mine !== undefined) { this.setDraft(r.mine); this.ready.set(true); }
        if (r.reviews === null || r.mine === undefined) this.error.set('Some reviews could not be loaded. Product browsing is still available.');
      });
      onCleanup(() => sub.unsubscribe());
    });
  }
  private setDraft(r: Review | null) { this.mine.set(r); this.rating = r?.rating || 5; this.body = r?.body || ''; }
  private path(publicRead: boolean) { return '/commerce/discovery/' + (publicRead ? 'public/' : '') + 'tenants/' + encodeURIComponent(this.tenant()) + '/discovery/products/' + encodeURIComponent(this.product()); }
  async signIn() { try { await this.session.signInProduct(this.tenant(), this.store(), this.productSlug()); } catch { this.error.set('Sign-in could not be started.'); } }
  async save() {
    if (this.busy() || !this.ready() || !this.canReview() || !this.body.trim()) return;
    this.busy.set(true); this.error.set('');
    const path = this.path(false);
    try {
      const review = await firstValueFrom(this.http.put<Review>(path + '/my-review', { rating: this.rating, body: this.body.trim(), version: this.mine()?.version || null }).pipe(timeout(10000)));
      if (path === this.path(false)) { this.setDraft(review); this.reviews.set(await firstValueFrom(this.http.get<PublicReview[]>(this.path(true) + '/reviews').pipe(timeout(10000)))); }
    } catch (e) {
      if (path === this.path(false)) this.error.set(e instanceof HttpErrorResponse && e.error?.title === 'VerifiedPurchaseRequired'
        ? 'You can review this item after a purchase on this account is delivered.' : 'The review could not be saved. Refresh and try again.');
    } finally { this.busy.set(false); }
  }
}
