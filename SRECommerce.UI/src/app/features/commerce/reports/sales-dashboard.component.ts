import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom, timeout } from 'rxjs';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
import { ORDER_STATUSES } from '../../../core/models/commerce-order.models';

interface SalesReport {
  from: string; through: string; currency: string; includesHistorical: boolean; timeZone: string; generatedAt: string;
  totals: { placedOrders: number; acceptedOrders: number; guestOrders: number; historicalOrders: number; acceptedOrderTotal: number; refundsForPlacedOrders: number; capturedStateBalance: number; };
  days: { date: string; placedOrders: number; acceptedOrders: number; acceptedOrderTotal: number; refundsForPlacedOrders: number; }[];
  states: { status: number; orders: number; orderTotal: number; }[];
  products: { productId: string; variantId: string; name: string; sku: string; quantity: number; lineTotal: number; }[];
}
@Component({ selector: 'app-sales-dashboard', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './sales-dashboard.component.html', styleUrl: './sales-dashboard.component.scss' })
export class SalesDashboardComponent {
  readonly session = inject(CommerceSessionService); private readonly http = inject(HttpClient); private readonly route = inject(ActivatedRoute);
  readonly tenant = this.route.snapshot.paramMap.get('tenant') || '';
  readonly report = signal<SalesReport | null>(null); readonly busy = signal(false); readonly error = signal(''); readonly states = ORDER_STATUSES;
  readonly canRead = computed(() => { const a = this.session.access(); return !!this.session.token && !!a && (a.isPlatformAdmin || a.selectedTenantId === this.tenant && ['Owner', 'Manager'].includes(a.tenantRole || '')); });
  readonly today = new Date().toISOString().slice(0, 10);
  from = new Date(Date.now() - 29 * 86400000).toISOString().slice(0, 10); through = this.today; currency = 'INR'; includeHistorical = false; reason = '';
  readonly peak = computed(() => Math.max(1, ...(this.report()?.days.map(day => day.acceptedOrderTotal) ?? [])));
  constructor() { if (typeof window !== 'undefined' && this.canRead() && !this.session.access()?.isPlatformAdmin) void this.load(); }
  async signIn() { try { await this.session.signIn(this.tenant, 'reports'); } catch { this.error.set('Sign-in could not be started.'); } }
  async load() {
    if (!this.canRead() || this.busy() || this.session.access()?.isPlatformAdmin && !this.reason.trim()) return;
    this.busy.set(true); this.error.set(''); this.report.set(null);
    try { this.report.set(await firstValueFrom(this.http.get<SalesReport>('/commerce/order/tenants/' + encodeURIComponent(this.tenant) + '/orders/reports/sales', {
      params: { from: this.from, through: this.through, currency: this.currency.trim().toUpperCase(), includeHistorical: this.includeHistorical },
      headers: this.session.access()?.isPlatformAdmin ? { 'X-Support-Reason': this.reason.trim() } : {}
    }).pipe(timeout(15000)))); }
    catch (e) { this.error.set(e instanceof HttpErrorResponse && e.status === 400 ? 'Choose a valid date range of up to 366 days ending today or earlier, and a three-letter currency.' : 'Sales data could not be loaded. Check your membership and try again.'); }
    finally { this.busy.set(false); }
  }
}
