import { Component, DestroyRef, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
import { MerchantPaymentService, MerchantPayment, PaymentConfiguration, PaymentHistory, PaymentReconciliation } from '../../../core/services/merchant-payment.service';

@Component({ selector: 'app-payments-workspace', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './payments-workspace.component.html', styleUrl: './payments-workspace.component.scss' })
export class PaymentsWorkspaceComponent {
  readonly session = inject(CommerceSessionService); private readonly api = inject(MerchantPaymentService); private readonly destroy = inject(DestroyRef);
  readonly tenant = inject(ActivatedRoute).snapshot.paramMap.get('tenant') || '';
  readonly busy = signal(false); readonly error = signal(''); readonly message = signal(''); readonly payments = signal<MerchantPayment[]>([]);
  readonly total = signal(0); readonly selected = signal<MerchantPayment | null>(null); readonly history = signal<PaymentHistory | null>(null);
  readonly reconciliation = signal<PaymentReconciliation | null>(null);
  readonly states = ['Pending', 'Authorized', 'Captured', 'Cancelled', 'Failed', 'Partially refunded', 'Refunded', 'Cash on delivery'];
  configuration: PaymentConfiguration | null = null; supportReason = ''; status: number | null = null; page = 1; outcome = 'Authorized';
  private pendingDecision: { signature: string; id: string } | null = null;
  get canRead() { const a = this.session.access(); return !!a && (a.isPlatformAdmin || a.selectedTenantId === this.tenant && ['Owner', 'Manager', 'Staff'].includes(a.tenantRole || '')); }
  get canManage() { const a = this.session.access(); return !!a && (a.isPlatformAdmin || a.selectedTenantId === this.tenant && ['Owner', 'Manager'].includes(a.tenantRole || '')); }
  get canConfigure() { return !!this.session.access()?.isPlatformAdmin || this.canManage && this.session.access()?.tenantRole === 'Owner'; }
  constructor() { if (this.session.token && this.canRead && !this.session.access()?.isPlatformAdmin) this.reload(); }
  async signIn() { try { await this.session.signIn(this.tenant, 'payments'); } catch { this.error.set('Sign-in could not be started. Please retry.'); } }
  private start() {
    if (!this.canRead || this.busy()) return false;
    if (this.session.access()?.isPlatformAdmin && !this.supportReason.trim()) { this.error.set('Enter a business support reason.'); return false; }
    this.busy.set(true); this.error.set(''); this.message.set(''); return true;
  }
  private failure(error: unknown) {
    const e = error as HttpErrorResponse;
    this.error.set(e.status === 401 ? 'Sign in again to continue.' : e.status === 403 ? 'Your role cannot access payments, or the module is disabled.' : e.status === 409 ? 'The payment changed or the operation conflicts. Refresh before starting a new action.' : 'The payment service could not complete the request. You can retry the same action.');
    this.busy.set(false);
  }
  reload() {
    if (!this.start()) return;
    this.api.list(this.tenant, this.page, this.status, this.supportReason).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: p => {
      this.payments.set(p.items); this.total.set(p.total); const selected = this.selected(); if (selected) this.selected.set(p.items.find(i => i.id === selected.id) || selected); this.busy.set(false);
    }, error: e => this.failure(e) });
  }
  filter() { this.page = 1; this.selected.set(null); this.history.set(null); this.reconciliation.set(null); this.reload(); }
  paginate(delta: number) { this.page += delta; this.reload(); }
  open(payment: MerchantPayment) {
    if (!this.start()) return; this.selected.set(payment); this.history.set(null); this.reconciliation.set(null);
    this.api.history(this.tenant, payment.orderId, this.supportReason).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: h => { this.history.set(h); this.busy.set(false); }, error: e => this.failure(e) });
  }
  loadConfiguration() {
    if (!this.canConfigure || !this.start()) return;
    this.api.configuration(this.tenant, this.supportReason).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: c => { this.configuration = c; this.busy.set(false); }, error: e => {
      if ((e as HttpErrorResponse).status === 404) { this.configuration = { provider: 'Sandbox', active: true, allowCard: true, allowUPI: true, allowNetBanking: true, allowCOD: false, outcome: 0, version: null, sandbox: true }; this.busy.set(false); }
      else this.failure(e);
    } });
  }
  saveConfiguration() {
    if (!this.configuration || !this.canConfigure || !this.start()) return;
    this.api.saveConfiguration(this.tenant, { ...this.configuration }, this.supportReason).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: c => { this.configuration = c; this.busy.set(false); this.message.set('Sandbox payment settings saved.'); }, error: e => this.failure(e) });
  }
  reconcile() {
    const p = this.selected(); if (!p || !this.canManage || !this.start()) return;
    this.api.reconcile(this.tenant, p.orderId, this.supportReason).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: r => { this.reconciliation.set(r); this.busy.set(false); }, error: e => this.failure(e) });
  }
  decide() {
    const p = this.selected(); if (!p || !p.sandbox || p.status !== 0 || !this.canManage || !this.start()) return;
    const signature = JSON.stringify([p.id, p.version, this.outcome]);
    if (signature !== this.pendingDecision?.signature) this.pendingDecision = { signature, id: crypto.randomUUID() };
    this.api.decide(this.tenant, p, this.pendingDecision.id, this.outcome, this.supportReason).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: result => {
      this.pendingDecision = null; this.busy.set(false); this.message.set(result.applied ? 'Sandbox decision verified. Refresh to see checkout progress.' : 'This payment was already finalized. Refresh its current state.');
    }, error: e => this.failure(e) });
  }
}
