import { Component, DestroyRef, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, Observable } from 'rxjs';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
import { MerchantOrderService } from '../../../core/services/merchant-order.service';
import { CommerceOrder, CommerceOrderHistory, CommerceOrderPage, CommerceOrderPolicy, ORDER_STATUSES, PAYMENT_STATES } from '../../../core/models/commerce-order.models';

@Component({ selector: 'app-orders-workspace', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './orders-workspace.component.html', styleUrl: './orders-workspace.component.scss' })
export class OrdersWorkspaceComponent {
  readonly session = inject(CommerceSessionService);
  private readonly api = inject(MerchantOrderService);
  private readonly destroy = inject(DestroyRef);
  readonly tenant = inject(ActivatedRoute).snapshot.paramMap.get('tenant') || '';
  readonly busy = signal(false); readonly error = signal(''); readonly message = signal('');
  readonly result = signal<CommerceOrderPage>({ items: [], total: 0, page: 1, pageSize: 50 });
  readonly selected = signal<CommerceOrder | null>(null); readonly history = signal<CommerceOrderHistory[]>([]);
  readonly statuses = ORDER_STATUSES; readonly payments = PAYMENT_STATES;
  policy: CommerceOrderPolicy | null = null;
  supportReason = ''; status: number | null = null; page = 1; note = ''; invoiceReference = '';
  private pending: { signature: string; operationId: string } | null = null;
  get canRead() {
    const a = this.session.access();
    return !!a && (a.isPlatformAdmin || a.selectedTenantId === this.tenant && ['Owner', 'Manager', 'Staff'].includes(a.tenantRole || ''));
  }
  get canManage() {
    const a = this.session.access();
    return !!a && (a.isPlatformAdmin || a.selectedTenantId === this.tenant && ['Owner', 'Manager'].includes(a.tenantRole || ''));
  }
  get canConfigure() { return this.session.access()?.isPlatformAdmin || this.session.access()?.tenantRole === 'Owner' && this.canManage; }
  get canCancel() { const o = this.selected(); return !!o && !o.historical && [0, 1, 2, 9].includes(o.status) && o.items.every(i => i.shipmentRequested === 0); }
  constructor() { if (this.session.token && this.canRead && !this.session.access()?.isPlatformAdmin) this.reload(); }
  async signIn() { try { await this.session.signIn(this.tenant, 'orders'); } catch { this.error.set('Sign-in could not be started. Please retry.'); } }
  private allowed() {
    if (!this.canRead || this.busy()) return false;
    if (this.session.access()?.isPlatformAdmin && !this.supportReason.trim()) { this.error.set('Enter a business support reason.'); return false; }
    this.busy.set(true); this.error.set(''); return true;
  }
  private failure(error: unknown) {
    const e = error as HttpErrorResponse;
    this.error.set(e.status === 401 ? 'Sign in again to continue.' : e.status === 403 ? 'Your role cannot access orders, or the module is disabled.' : e.status === 409 ? 'The order changed or this action conflicts with its current state. Refresh before starting a new action.' : 'Orders could not be loaded or updated. Retry when the service is available.');
    this.busy.set(false);
  }
  reload() {
    if (!this.allowed()) return;
    this.api.list(this.tenant, this.page, this.status, this.supportReason).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: value => { this.result.set(value); this.busy.set(false); }, error: e => this.failure(e)
    });
  }
  open(order: CommerceOrder) {
    if (!this.allowed()) return;
    forkJoin({ order: this.api.order(this.tenant, order.id, this.supportReason), history: this.api.history(this.tenant, order.id, this.supportReason) })
      .pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: r => { this.selected.set(r.order); this.history.set(r.history); this.invoiceReference = r.order.invoiceReference || ''; this.busy.set(false); }, error: e => this.failure(e) });
  }
  changePage(delta: number) { this.page += delta; this.reload(); }
  filter() { this.page = 1; this.selected.set(null); this.reload(); }
  loadPolicy() {
    if (!this.canConfigure || !this.allowed()) return;
    this.api.policy(this.tenant, this.supportReason).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: p => { this.policy = p; this.busy.set(false); }, error: e => this.failure(e) });
  }
  savePolicy() {
    if (!this.policy || !this.canConfigure || !this.allowed()) return;
    this.api.savePolicy(this.tenant, { ...this.policy }, this.supportReason).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: p => { this.policy = p; this.message.set('Order policy saved.'); this.busy.set(false); }, error: e => this.failure(e) });
  }
  private operation(kind: string, order: CommerceOrder, value: string) {
    const signature = JSON.stringify([kind, order.id, order.version, value.trim()]);
    if (this.pending?.signature !== signature) this.pending = { signature, operationId: crypto.randomUUID() };
    return this.pending.operationId;
  }
  private update(request: Observable<CommerceOrder>, message: string) {
    request.pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: o => { this.pending = null; this.selected.set(o); this.result.update(p => ({ ...p, items: p.items.map(row => row.id === o.id ? o : row) })); this.message.set(message); this.busy.set(false); }, error: e => this.failure(e) });
  }
  cancel() {
    const o = this.selected();
    if (!o || !this.canManage || !this.canCancel || !this.note.trim() || !this.allowed()) return;
    this.update(this.api.cancel(this.tenant, o, this.operation('cancel', o, this.note), this.note, this.supportReason), 'Cancellation requested. Refund and stock recovery run before cancellation is completed. Refresh to check progress.');
  }
  invoice() {
    const o = this.selected();
    if (!o || o.historical || !this.canManage || !this.invoiceReference.trim() || !this.allowed()) return;
    this.update(this.api.invoice(this.tenant, o, this.operation('invoice', o, this.invoiceReference), this.invoiceReference, this.supportReason), 'Invoice reference saved.');
  }
}
