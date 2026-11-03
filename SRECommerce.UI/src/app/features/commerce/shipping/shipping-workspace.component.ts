import { Component, DestroyRef, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { forkJoin, Observable } from 'rxjs';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
import { MerchantShippingService, ShippingZone, ShippingMethod, ShippingShipment, ShippingPickup, ShippingHistory } from '../../../core/services/merchant-shipping.service';
import { MerchantInventoryService } from '../../../core/services/merchant-inventory.service';

@Component({ selector: 'app-shipping-workspace', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './shipping-workspace.component.html', styleUrl: './shipping-workspace.component.scss' })
export class ShippingWorkspaceComponent {
  readonly session = inject(CommerceSessionService); private readonly api = inject(MerchantShippingService); private readonly inventory = inject(MerchantInventoryService); private readonly destroy = inject(DestroyRef);
  readonly tenant = inject(ActivatedRoute).snapshot.paramMap.get('tenant') || ''; readonly busy = signal(false); readonly error = signal(''); readonly message = signal('');
  readonly zones = signal<ShippingZone[]>([]); readonly methods = signal<ShippingMethod[]>([]); readonly shipments = signal<ShippingShipment[]>([]); readonly pickups = signal<ShippingPickup[]>([]);
  readonly warehouses = signal<{ id: string; name: string }[]>([]); readonly history = signal<ShippingHistory | null>(null); readonly total = signal(0);
  readonly states = ['Created', 'Packed', 'Dispatched', 'Delivered', 'Cancelled', 'Delivery exception']; readonly kinds = ['Local delivery', 'Store pickup', 'Sandbox courier'];
  supportReason = ''; currency = 'INR'; page = 1; status: number | null = null; note = ''; returnId = ''; pickupWarehouse = ''; pickupFrom = ''; pickupTo = '';
  zoneId: string | null = null; zone = { code: '', name: '', countries: 'IN', prefixes: '', active: true, version: null as string | null };
  methodId: string | null = null; method = this.emptyMethod();
  private readonly pending = new Map<string, { signature: string; id: string }>();
  get canRead() { const a = this.session.access(); return !!a && (a.isPlatformAdmin || a.selectedTenantId === this.tenant && ['Owner', 'Manager', 'Staff'].includes(a.tenantRole || '')); }
  get canManage() { const a = this.session.access(); return !!a && (a.isPlatformAdmin || a.selectedTenantId === this.tenant && ['Owner', 'Manager'].includes(a.tenantRole || '')); }
  get canConfigure() { return !!this.session.access()?.isPlatformAdmin || this.canManage && this.session.access()?.tenantRole === 'Owner'; }
  constructor() { if (this.session.token && this.canRead && !this.session.access()?.isPlatformAdmin) this.reload(); }
  private emptyMethod() { return { zoneId: '', code: '', name: '', description: '', kind: 0, flatFee: 0, perUnitFee: 0, taxRate: 0, freeAbove: null as number | null, minimumOrder: 0, maximumOrder: null as number | null, maximumUnits: 50000, pickupWarehouseId: null as string | null, active: true, version: null as string | null }; }
  async signIn() { try { await this.session.signIn(this.tenant, 'shipping'); } catch { this.error.set('Sign-in could not be started. Please retry.'); } }
  private start() { if (!this.canRead || this.busy()) return false; if (this.session.access()?.isPlatformAdmin && !this.supportReason.trim()) { this.error.set('Enter a business support reason.'); return false; } this.busy.set(true); this.error.set(''); return true; }
  private failure(error: unknown) { const e = error as HttpErrorResponse; this.error.set(e.status === 401 ? 'Sign in again to continue.' : e.status === 403 ? 'Your role cannot perform this action, or the required module is disabled.' : e.status === 409 ? 'Delivery data changed or this action conflicts. Refresh before starting a new operation.' : 'The request could not be completed. You can retry the same action.'); this.busy.set(false); }
  reload() {
    if (!this.start()) return;
    forkJoin({ lookups: this.api.lookups(this.tenant, this.supportReason), shipments: this.api.shipments(this.tenant, this.page, this.status, this.supportReason), pickups: this.api.pickups(this.tenant, this.supportReason) })
      .pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: r => { this.zones.set(r.lookups.zones); this.methods.set(r.lookups.methods); this.currency = r.lookups.currency; this.shipments.set(r.shipments.items); this.total.set(r.shipments.total); this.pickups.set(r.pickups); this.busy.set(false); }, error: e => this.failure(e) });
  }
  filter() { this.page = 1; this.reload(); }
  paginate(delta: number) { this.page += delta; this.reload(); }
  loadWarehouses() { if (!this.canManage || !this.start()) return; this.inventory.lookups(this.tenant, this.supportReason).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: l => { const branches = new Set(l.branches.filter(b => b.active).map(b => b.id)); this.warehouses.set(l.warehouses.filter(w => w.active && branches.has(w.branchId)).map(w => ({ id: w.id, name: w.name }))); this.busy.set(false); }, error: e => this.failure(e) }); }
  private key(label: string, body: unknown) { const signature = JSON.stringify(body) ?? 'null'; let pending = this.pending.get(label); if (!pending || pending.signature !== signature) { pending = { signature, id: crypto.randomUUID() }; this.pending.set(label, pending); } return pending.id; }
  private run<T>(label: string, request: Observable<T>, message: string, after?: (value: T) => void) {
    request.pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: value => { this.pending.delete(label); after?.(value); this.busy.set(false); this.message.set(message); this.reload(); }, error: e => this.failure(e) });
  }
  editZone(z: ShippingZone) { this.zoneId = z.id; this.zone = { code: z.code, name: z.name, countries: z.countries.join(', '), prefixes: z.postalPrefixes.join(', '), active: z.active, version: z.version }; }
  newZone() { this.zoneId = null; this.zone = { code: '', name: '', countries: 'IN', prefixes: '', active: true, version: null }; }
  saveZone() {
    if (!this.canConfigure || !this.start()) return;
    const body = { code: this.zone.code, name: this.zone.name, countries: this.zone.countries.split(',').map(s => s.trim()).filter(Boolean), postalPrefixes: this.zone.prefixes.split(',').map(s => s.trim()).filter(Boolean), active: this.zone.active, version: this.zone.version };
    const label = 'zone:' + (this.zoneId || 'new'); const operationId = this.key(label, body);
    this.run(label, this.api.saveZone(this.tenant, this.zoneId, { operationId, ...body }, this.supportReason), 'Delivery zone saved.', z => this.editZone(z));
  }
  editMethod(m: ShippingMethod) { this.methodId = m.id; this.method = { zoneId: m.zoneId, code: m.code, name: m.name, description: m.description, kind: m.kind, flatFee: m.flatFee, perUnitFee: m.perUnitFee, taxRate: m.taxRate, freeAbove: m.freeAbove, minimumOrder: m.minimumOrder, maximumOrder: m.maximumOrder, maximumUnits: m.maximumUnits, pickupWarehouseId: m.pickupWarehouseId, active: m.active, version: m.version }; }
  newMethod() { this.methodId = null; this.method = this.emptyMethod(); }
  saveMethod() {
    if (!this.canConfigure || !this.start()) return;
    const body = { ...this.method, currency: this.currency, pickupWarehouseId: this.method.kind === 1 ? this.method.pickupWarehouseId : null }; const label = 'method:' + (this.methodId || 'new');
    this.run(label, this.api.saveMethod(this.tenant, this.methodId, { operationId: this.key(label, body), ...body }, this.supportReason), 'Shipping method saved.', m => this.editMethod(m));
  }
  open(s: ShippingShipment) { if (!this.start()) return; this.api.history(this.tenant, s.id, this.supportReason).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: h => { this.history.set(h); this.busy.set(false); }, error: e => this.failure(e) }); }
  next(s: ShippingShipment) { return ({ 0: [1, 4], 1: [2, 4], 2: [3, 5], 5: [2, 3] } as Record<number, number[]>)[s.status] || []; }
  move(status: number) {
    const s = this.history()?.shipment; if (!s || !this.canManage || !this.note.trim() || !this.next(s).includes(status) || !this.start()) return;
    const body = { version: s.version, status, note: this.note }; const label = 'shipment:' + s.id + ':' + status;
    this.run(label, this.api.state(this.tenant, s.id, { operationId: this.key(label, body), ...body }, this.supportReason), 'Delivery movement recorded. The order update will follow.', result => { this.history.update(h => h ? { ...h, shipment: result } : h); });
  }
  retry(id: string) { if (!this.canManage || !this.start()) return; this.run('retry:' + id, this.api.retry(this.tenant, id, this.supportReason), 'Order update queued for retry.'); }
  schedule() {
    if (!this.canManage || !this.returnId.trim() || !this.pickupWarehouse || !this.pickupFrom || !this.pickupTo) return;
    const from = new Date(this.pickupFrom), to = new Date(this.pickupTo); if (!Number.isFinite(from.getTime()) || !Number.isFinite(to.getTime()) || to <= from) { this.error.set('Choose a valid pickup window.'); return; } if (!this.start()) return;
    const body = { returnId: this.returnId.trim(), warehouseId: this.pickupWarehouse, windowFrom: from.toISOString(), windowTo: to.toISOString() }; const label = 'pickup:' + body.returnId;
    this.run(label, this.api.schedule(this.tenant, { id: this.key(label, body), ...body }, this.supportReason), 'Return pickup scheduled. Stock receipt and refund require warehouse approval.');
  }
  pickupState(p: ShippingPickup, status: number) {
    if (!this.canManage || p.status !== 0 || !this.note.trim() || !this.start()) return;
    const body = { version: p.version, status, note: this.note }; const label = 'pickup-state:' + p.id + ':' + status;
    this.run(label, this.api.pickupState(this.tenant, p.id, { operationId: this.key(label, body), ...body }, this.supportReason), status === 1 ? 'Pickup collected for warehouse inspection.' : 'Pickup cancelled. A new pickup can be scheduled.');
  }
}
