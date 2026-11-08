import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom, timeout } from 'rxjs';
import { CommerceExchange, CommerceReturn } from '../../../core/models/commerce-checkout.models';
import { CommerceOrder } from '../../../core/models/commerce-order.models';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
import { MerchantInventoryService, StockWarehouse } from '../../../core/services/merchant-inventory.service';

interface Fulfillment { id: string; warehouseId: string; items: { variantId: string; quantity: number }[]; status: number; courierReference: string | null; version: string; }

@Component({
  selector: 'app-order-workflow', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './order-workflow.component.html',
  styles: [`:host{display:block;min-height:100vh;background:#f5f7fa;color:#192d3c}main{max-width:1050px;margin:auto;padding:2rem 1rem}
    article{background:white;border:1px solid #cddce6;border-radius:10px;padding:1rem;margin:1rem 0}label{display:block;margin:.4rem 0}input:not([type=checkbox]),select{display:block;padding:.5rem;border:1px solid #829aaa;border-radius:5px}
    button{padding:.55rem .8rem;margin:.35rem .4rem .35rem 0;background:#174b70;color:white;border:0;border-radius:5px}button:disabled{opacity:.5}[role=alert]{color:#982219}`]
})
export class OrderWorkflowComponent {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly session = inject(CommerceSessionService);
  private readonly inventory = inject(MerchantInventoryService);
  readonly tenant = this.route.snapshot.paramMap.get('tenant') || '';
  readonly orderId = this.route.snapshot.paramMap.get('order') || '';
  readonly order = signal<CommerceOrder | null>(null);
  readonly warehouses = signal<StockWarehouse[]>([]);
  readonly fulfillments = signal<Fulfillment[]>([]);
  readonly returns = signal<CommerceReturn[]>([]);
  readonly exchanges = signal<CommerceExchange[]>([]);
  readonly busy = signal(false); readonly error = signal(''); readonly message = signal('');
  readonly parcelStates = ['Requested','Created','Shipped','Delivered','Cancelled','Failed'];
  readonly returnStates = ['Requested','Approved','Received','Refund pending','Refunded','Rejected','Cancelled','Receiving','Needs review'];
  readonly exchangeStates = ['Requested','Approved','Return received','Replacement pending','Completed','Rejected','Cancelled'];
  readonly quantities: Record<string, number> = {};
  readonly returnWarehouse: Record<string, string> = {};
  readonly resalable: Record<string, boolean> = {};
  readonly replacement: Record<string, string> = {};
  readonly pendingCount = signal(0);
  private readonly writes = new Map<string, { id: string; field: 'id' | 'operationId'; signature: string }>();
  supportReason = ''; warehouse = '';
  get support() { return !!this.session.access()?.isPlatformAdmin; }
  get role() { const a = this.session.access(); return this.session.token && a?.selectedTenantId === this.tenant ? a.tenantRole : null; }
  get allowed() { return this.support || ['Owner','Manager','Staff'].includes(this.role || ''); }
  get canManage() { return this.support || ['Owner','Manager'].includes(this.role || ''); }
  get canFulfill() { const order = this.order(); return this.canManage && !!order && !order.historical && order.paymentMethod !== 'CounterCash' && [1,2,3].includes(order.status); }
  get canHandover() {
    const order = this.order();
    return this.allowed && !this.support && order?.paymentMethod === 'CounterCash' && order.status === 1 && order.paymentState === 2;
  }
  constructor() { if (typeof window !== 'undefined' && this.allowed && !this.support) void this.reload(); }
  private orderPath() { return 'orders/' + encodeURIComponent(this.orderId); }
  private request<T>(method: 'GET' | 'POST', path: string, body?: unknown) {
    const url = '/commerce/order/tenants/' + encodeURIComponent(this.tenant) + '/order-management/' + path;
    return firstValueFrom(this.http.request<T>(method, url, { body, headers: this.support ? { 'X-Support-Reason': this.supportReason.trim() } : {} }).pipe(timeout(12000)));
  }
  async signIn() { try { await this.session.signIn(this.tenant, 'orders'); } catch { this.error.set('Sign-in could not be started.'); } }
  private async run(action: () => Promise<void>) {
    if (!this.allowed || this.busy() || this.support && !this.supportReason.trim()) return;
    this.busy.set(true); this.error.set(''); this.message.set('');
    try { await action(); }
    catch (e) { this.error.set(e instanceof HttpErrorResponse && e.status === 409
      ? 'The operation conflicts with current stock, shipment or return status. Reload and check the current order.'
      : 'The order workflow request could not be confirmed. Refresh before changing the requested action.'); }
    finally { this.busy.set(false); }
  }
  private async load() {
    const root = this.orderPath();
    const [order, parcels, returns, exchanges, inventory] = await Promise.all([
      this.request<CommerceOrder>('GET',root), this.request<Fulfillment[]>('GET',root+'/fulfillments'),
      this.request<CommerceReturn[]>('GET',root+'/returns'), this.request<CommerceExchange[]>('GET',root+'/exchanges'),
      firstValueFrom(this.inventory.lookups(this.tenant,this.supportReason)).catch(() => null)
    ]);
    this.order.set(order); this.fulfillments.set(parcels); this.returns.set(returns); this.exchanges.set(exchanges); this.warehouses.set(inventory?.warehouses.filter(w => w.active) || []);
  }
  reload() { return this.run(() => this.load()); }
  sku(id: string) { return this.order()?.items.find(line => line.variantId === id)?.sku || id; }
  warehouseName(id: string) { return this.warehouses().find(x => x.id === id)?.name || id; }
  private async write(path: string, field: 'id' | 'operationId', value: object) {
    const signature = JSON.stringify(value); let prior = this.writes.get(path);
    if (prior && prior.signature !== signature) throw new Error('CheckOriginalOperationFirst');
    if (!prior) { prior = { id: crypto.randomUUID(), field, signature }; this.writes.set(path, prior); this.pendingCount.set(this.writes.size); }
    await this.sendPending(path, prior);
  }
  private async sendPending(path: string, prior: { id: string; field: 'id' | 'operationId'; signature: string }) {
    try { await this.request('POST',path,{ [prior.field]: prior.id, ...JSON.parse(prior.signature) }); this.writes.delete(path); }
    catch (e) { if (e instanceof HttpErrorResponse && e.status >= 400 && e.status < 500 && e.status !== 429) this.writes.delete(path); throw e; }
    finally { this.pendingCount.set(this.writes.size); }
  }
  retryUnconfirmed() { return this.run(async () => {
    for (const [path, prior] of this.writes) {
      if (this.canManage || this.allowed && !this.support && path.endsWith('/counter-handover')) await this.sendPending(path, prior);
    }
    await this.load(); this.message.set('Previously submitted operations checked.');
  }); }
  handover() { return this.run(async () => {
    const order = this.order();
    if (!order || !this.canHandover) return;
    await this.write(this.orderPath() + '/counter-handover', 'operationId', { version: order.version });
    await this.load(); this.message.set('Counter handover recorded.');
  }); }
  fulfill() { return this.run(async () => {
    const order = this.order(); if (!order || !this.canFulfill || !this.warehouse) return;
    const items = order.items.map(x => ({ variantId: x.variantId, quantity: this.quantities[x.variantId] || 0 })).filter(x => x.quantity > 0);
    if (!items.length || items.some(x => x.quantity > 1000 || x.quantity > order.items.find(i => i.variantId === x.variantId)!.quantity - order.items.find(i => i.variantId === x.variantId)!.shipmentRequested))
      { this.error.set('Enter a quantity within the unrequested amount for each parcel item.'); return; }
    await this.write(this.orderPath() + '/fulfillments','id',{ version: order.version, warehouseId: this.warehouse, items });
    for (const line of items) delete this.quantities[line.variantId];
    await this.load(); this.message.set('Parcel requested. Shipping will prepare it from the committed stock.');
  }); }
  changeReturn(r: CommerceReturn, status: number) { return this.run(async () => {
    if (!this.canManage) return;
    const warehouseId = status === 2 ? this.returnWarehouse[r.id] : null;
    if (status === 2 && !warehouseId) return;
    await this.write('returns/' + encodeURIComponent(r.id) + '/status','operationId',
      { version: r.version, status, warehouseId, resalable: status === 2 && !!this.resalable[r.id] });
    await this.load(); this.message.set(status === 2 ? 'Receipt recorded. Stock and refund processing will continue.' : 'Return decision saved.');
  }); }
  changeExchange(x: CommerceExchange, status: number) { return this.run(async () => {
    if (!this.canManage) return;
    const replacementOrderId = status === 3 ? this.replacement[x.id]?.trim() : null;
    if (status === 3 && !/^[a-f0-9-]{36}$/i.test(replacementOrderId || '')) { this.error.set('Enter the replacement order ID.'); return; }
    await this.write('exchanges/' + encodeURIComponent(x.id) + '/status','operationId',{ version: x.version, status, replacementOrderId });
    await this.load(); this.message.set('Exchange updated.');
  }); }
}
