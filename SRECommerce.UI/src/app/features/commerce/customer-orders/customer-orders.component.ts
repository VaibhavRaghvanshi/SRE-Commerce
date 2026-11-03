import { Component, DestroyRef, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CommerceCatalogService } from '../../../core/services/commerce-catalog.service';
import { CommerceCheckoutService } from '../../../core/services/commerce-checkout.service';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
import { CommerceTenant } from '../../../core/models/commerce.models';
import { CommerceOrder, CommerceOrderHistory, ORDER_STATUSES, PAYMENT_STATES } from '../../../core/models/commerce-order.models';
import { CommerceExchange, CommerceReturn, CommerceTracking, SavedOrderReference } from '../../../core/models/commerce-checkout.models';

@Component({ selector: 'app-commerce-customer-orders', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './customer-orders.component.html', styleUrl: './customer-orders.component.scss' })
export class CommerceCustomerOrdersComponent {
  readonly session = inject(CommerceSessionService); private readonly catalog = inject(CommerceCatalogService); private readonly api = inject(CommerceCheckoutService); private readonly route = inject(ActivatedRoute);
  readonly tenant = signal<CommerceTenant | null>(null); readonly orders = signal<CommerceOrder[]>([]); readonly order = signal<CommerceOrder | null>(null); readonly refs = signal<SavedOrderReference[]>([]);
  readonly events = signal<CommerceOrderHistory[]>([]); readonly tracking = signal<CommerceTracking[]>([]); readonly returns = signal<CommerceReturn[]>([]); readonly exchanges = signal<CommerceExchange[]>([]);
  readonly candidates = signal<{ id: string; label: string }[]>([]); readonly busy = signal(true); readonly error = signal(''); readonly message = signal(''); readonly total = signal(0);
  readonly states = ORDER_STATUSES; readonly paymentStates = PAYMENT_STATES; readonly shipmentStates = ['Created', 'Packed', 'Dispatched', 'Delivered', 'Cancelled', 'Delivery exception'];
  readonly returnStates = ['Requested', 'Approved', 'Received', 'Refund pending', 'Refunded', 'Rejected', 'Cancelled', 'Receiving', 'Needs review'];
  readonly exchangeStates = ['Requested', 'Approved', 'Return received', 'Replacement pending', 'Completed', 'Rejected', 'Cancelled'];
  readonly detailId = this.route.snapshot.paramMap.get('order'); page = 1; reason = ''; variantId = ''; quantity = 1; mode: 'return' | 'exchange' = 'return'; replacementId = ''; replacementSearch = '';
  private destroyed = false;
  get customer() { const tenant = this.tenant(); return !!this.session.token && !!tenant && this.session.access()?.selectedTenantId === tenant.tenantId; }
  get returnable() { return this.order()?.items.filter(i => i.delivered > i.returnRequested) || []; }
  get canCancel() { const order = this.order(); return !!order && !order.historical && [1, 2].includes(order.status) && order.items.every(i => i.shipmentRequested === 0); }
  constructor() { inject(DestroyRef).onDestroy(() => this.destroyed = true); if (typeof window !== 'undefined') void this.initialize(); }
  private async initialize() {
    try { const tenant = await firstValueFrom(this.catalog.tenant(this.route.snapshot.paramMap.get('slug') || '')); this.tenant.set(tenant); this.refs.set(this.api.references(tenant.tenantId)); await this.load(); }
    catch { this.error.set('Order history could not be loaded. Check your sign-in or saved guest order reference.'); } finally { this.busy.set(false); }
  }
  private async load() {
    const tenant = this.tenant(); if (!tenant || this.destroyed) return;
    if (this.detailId) {
      const order = await this.api.order(tenant.tenantId, this.detailId); this.order.set(order);
      const data = await Promise.allSettled([this.api.history(tenant.tenantId, order.id), this.api.tracking(tenant.tenantId, order.id), this.api.returns(tenant.tenantId, order.id), this.api.exchanges(tenant.tenantId, order.id)] as const);
      if (this.destroyed) return;
      this.events.set(data[0].status === 'fulfilled' ? data[0].value : []); this.tracking.set(data[1].status === 'fulfilled' ? data[1].value : []);
      this.returns.set(data[2].status === 'fulfilled' ? data[2].value : []); this.exchanges.set(data[3].status === 'fulfilled' ? data[3].value : []);
      if (data.some(r => r.status === 'rejected')) this.error.set('Some tracking or return details are temporarily unavailable. Refresh to check again.');
    } else if (this.customer) { const result = await this.api.list(tenant.tenantId, this.page); this.orders.set(result.items); this.total.set(result.total); }
  }
  async refresh(delta = 0) { if (this.busy()) return; this.page = Math.max(1, this.page + delta); this.busy.set(true); this.error.set(''); try { await this.load(); } catch { this.error.set('The latest order details could not be loaded.'); } finally { this.busy.set(false); } }
  async signIn() { const tenant = this.tenant(); if (tenant) try { await this.session.signInStore(tenant.tenantId, tenant.slug, 'orders'); } catch { this.error.set('Sign-in could not be started.'); } }
  private async action(work: () => Promise<unknown>, message: string) {
    if (this.busy()) return; this.busy.set(true); this.error.set(''); this.message.set('');
    try { await work(); this.message.set(message); this.reason = ''; await this.load(); }
    catch { this.error.set('The change could not be confirmed. Retry the same request or refresh the order to check its current state.'); } finally { this.busy.set(false); }
  }
  cancel() { const tenant = this.tenant(), order = this.order(); if (!tenant || !order || !this.canCancel || !this.reason.trim()) return; void this.action(() => this.api.cancel(tenant.tenantId, order, this.reason), 'Cancellation requested. The store will update your order after any payment and stock recovery.'); }
  async findReplacement() {
    const tenant = this.tenant(); if (!tenant || this.busy() || !this.replacementSearch.trim()) return; this.busy.set(true); this.error.set(''); this.replacementId = '';
    try { const result = await firstValueFrom(this.catalog.products(tenant.tenantId, 1, this.replacementSearch.trim(), '', '', '')); this.candidates.set(result.items.flatMap(p => p.variants.filter(v => v.active && v.id).map(v => ({ id: v.id, label: p.name + ' · ' + v.sku })))); }
    catch { this.error.set('Replacement products could not be loaded.'); } finally { this.busy.set(false); }
  }
  requestReturn() {
    const tenant = this.tenant(), order = this.order(), line = this.returnable.find(i => i.variantId === this.variantId); const qty = Number(this.quantity);
    if (!tenant || !order || order.historical || !line || !this.reason.trim() || !Number.isFinite(qty) || qty <= 0 || qty > line.delivered - line.returnRequested || Math.abs(qty * 1000 - Math.round(qty * 1000)) > 0.000001) return;
    if (this.mode === 'exchange') { if (!this.replacementId) return; void this.action(() => this.api.exchange(tenant.tenantId, order, line.variantId, this.replacementId, qty, this.reason), 'Exchange requested. The store will review your return and replacement.'); }
    else void this.action(() => this.api.return(tenant.tenantId, order, line.variantId, qty, this.reason), 'Return requested. The store will review the items before processing a refund.');
  }
  replacementAvailable(id: string) { return this.customer || this.refs().some(r => r.id === id); }
}
