import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom, timeout } from 'rxjs';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
import { InventoryLookups, MerchantInventoryService } from '../../../core/services/merchant-inventory.service';

interface Material { variantId: string; perUnit: number; standardUnitCost: number; }
interface Bom { id: string; code: string; name: string; revision: number; outputVariantId: string; currency: string; materials: Material[]; stages: string[]; }
interface Order { id: string; bomId: string; warehouseId: string; outputVariantId: string; plannedQuantity: number; state: number; version: string;
  stages: string[]; stagesCompleted: number; qualityChecked: boolean; qualityPassed: boolean; inputsConsumed: boolean;
  materials: { variantId: string; quantity: number }[]; currency: string; standardMaterialCost: number; overheadCost: number;
  goodQuantity: number; wasteQuantity: number; outputLot: string | null; lastFault: string | null; createdAt: string; }
interface History { state: number; action: string; note: string; createdAt: string; }

@Component({
  selector: 'app-manufacturing', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './manufacturing.component.html',
  styles: [`:host{display:block;background:#f5f7fa;color:#182a39;min-height:100vh}main{max-width:1150px;margin:auto;padding:2rem 1rem}
    article{background:white;border:1px solid #cddcea;border-radius:12px;margin:1rem 0;padding:1rem}fieldset{border:1px solid #cad9e5;margin:.5rem 0}
    .fields{display:flex;gap:.8rem;flex-wrap:wrap;align-items:end}label{display:block;margin:.4rem 0}input,select,textarea{display:block;padding:.5rem;border:1px solid #829bad;border-radius:5px;max-width:100%}
    button{background:#184b72;color:white;border:0;border-radius:5px;padding:.55rem .7rem;margin:.35rem .4rem .35rem 0}button:disabled{opacity:.5}
    table{width:100%;border-collapse:collapse}td,th{text-align:left;border-bottom:1px solid #cddcea;padding:.45rem}.table{overflow:auto}[role=alert]{color:#992819}`]
})
export class ManufacturingComponent {
  private readonly http = inject(HttpClient);
  private readonly inventory = inject(MerchantInventoryService);
  private readonly session = inject(CommerceSessionService);
  readonly tenant = inject(ActivatedRoute).snapshot.paramMap.get('tenant') || '';
  readonly lookups = signal<InventoryLookups | null>(null);
  readonly boms = signal<Bom[]>([]);
  readonly orders = signal<Order[]>([]);
  readonly active = signal<Order | null>(null);
  readonly history = signal<History[]>([]);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly message = signal('');
  readonly states = ['Planned', 'Reserving materials', 'In production', 'Consuming materials', 'Receiving finished stock', 'Completed', 'Cancelling', 'Cancelled', 'Needs review'];
  reason = ''; note = ''; code = ''; name = ''; output = ''; stageText = ''; bomId = ''; warehouse = ''; planned = 1; good = 0; waste = 0; overhead = 0; expiry = '';
  materials: Material[] = [{ variantId: '', perUnit: 1, standardUnitCost: 0 }];
  private readonly pending = new Map<string, { id: string; signature: string }>();
  get support() { return !!this.session.access()?.isPlatformAdmin; }
  get role() { const a = this.session.access(); return this.session.token && a?.selectedTenantId === this.tenant ? a.tenantRole : null; }
  get read() { return this.support || ['Owner', 'Manager', 'Staff'].includes(this.role || ''); }
  get manage() { return this.support || ['Owner', 'Manager'].includes(this.role || ''); }
  constructor() { if (typeof window !== 'undefined' && this.read && !this.support) void this.reload(); }
  private url(path: string) { return '/commerce/manufacturing/tenants/' + encodeURIComponent(this.tenant) + '/manufacturing/' + path; }
  private request<T>(method: 'GET' | 'POST', path: string, body?: unknown) {
    return firstValueFrom(this.http.request<T>(method, this.url(path), { body, headers: this.support ? { 'X-Support-Reason': this.reason.trim() } : {} }).pipe(timeout(10000)));
  }
  async signIn() { try { await this.session.signIn(this.tenant, 'manufacturing'); } catch { this.error.set('Sign-in could not be started.'); } }
  private async run(work: () => Promise<void>) {
    if (!this.read || this.busy() || this.support && !this.reason.trim()) return;
    this.busy.set(true); this.error.set(''); this.message.set('');
    try { await work(); }
    catch (e) { this.error.set(e instanceof HttpErrorResponse && e.status === 409 ?
      'The operation could not complete with the current stock or version. Refresh production status before retrying.' :
      'Manufacturing could not complete this request. Check the selected materials, warehouse and access.'); }
    finally { this.busy.set(false); }
  }
  reload() { return this.run(async () => {
    const [boms, orders, stock] = await Promise.all([this.request<Bom[]>('GET', 'boms'), this.request<Order[]>('GET', 'production'),
      firstValueFrom(this.inventory.lookups(this.tenant, this.reason).pipe(timeout(10000)))]);
    this.boms.set(boms); this.orders.set(orders); this.lookups.set(stock);
    if (this.active()) await this.open(this.active()!.id);
  }); }
  variant(id: string) { return this.lookups()?.items.find(x => x.id === id)?.sku || id; }
  addMaterial() { if (this.materials.length < 40) this.materials.push({ variantId: '', perUnit: 1, standardUnitCost: 0 }); }
  private async create<T>(path: string, payload: object) {
    const signature = JSON.stringify(payload); let pending = this.pending.get(path);
    if (pending && pending.signature !== signature) throw new Error('RetryOriginalCreate');
    if (!pending) { pending = { id: crypto.randomUUID(), signature }; this.pending.set(path, pending); }
    try { const created = await this.request<T>('POST', path, { ...payload, id: pending.id }); this.pending.delete(path); return created; }
    catch (e) { if (e instanceof HttpErrorResponse && e.status >= 400 && e.status < 500 && e.status !== 429) this.pending.delete(path); throw e; }
  }
  saveBom() { return this.run(async () => {
    if (!this.manage) return;
    const bom = await this.create<Bom>('boms', { code: this.code.trim(), name: this.name.trim(), outputVariantId: this.output,
      materials: this.materials, stages: this.stageText.split('\n').map(x => x.trim()).filter(Boolean) });
    this.boms.update(rows => [bom, ...rows.filter(x => x.id !== bom.id)]); this.bomId = bom.id; this.message.set('BOM revision saved.');
  }); }
  plan() { return this.run(async () => {
    if (!this.manage) return;
    const order = await this.create<Order>('production', { bomId: this.bomId, warehouseId: this.warehouse, quantity: this.planned });
    this.update(order); await this.open(order.id); this.message.set('Production order planned.');
  }); }
  private update(order: Order) {
    this.orders.update(rows => rows.some(x => x.id === order.id) ? rows.map(x => x.id === order.id ? order : x) : [order, ...rows]);
    if (this.active()?.id !== order.id) { this.good = order.plannedQuantity; this.waste = 0; this.overhead = 0; this.expiry = ''; this.note = ''; }
    this.active.set(order);
  }
  private async open(id: string) {
    const [order, history] = await Promise.all([this.request<Order>('GET', 'production/' + id), this.request<History[]>('GET', 'production/' + id + '/history')]);
    this.update(order); this.history.set(history);
  }
  select(id: string) { return this.run(() => this.open(id)); }
  action(path: 'start' | 'stage' | 'quality' | 'finish' | 'cancel' | 'retry', passed?: boolean) { return this.run(async () => {
    const order = this.active();
    if (!order || !this.note.trim() || !this.manage && path !== 'stage' && path !== 'quality') return;
    const base = { version: order.version, note: this.note.trim() };
    const payload = path === 'quality' ? { ...base, passed } : path === 'finish' ? { ...base, goodQuantity: this.good, wasteQuantity: this.waste,
      overheadCost: this.overhead, expiresAt: this.expiry ? new Date(this.expiry).toISOString() : null } : base;
    const changed = await this.request<Order>('POST', 'production/' + order.id + '/' + path, payload);
    this.note = ''; this.update(changed); this.history.set(await this.request<History[]>('GET', 'production/' + changed.id + '/history'));
    this.message.set('Production status updated.');
  }); }
}
