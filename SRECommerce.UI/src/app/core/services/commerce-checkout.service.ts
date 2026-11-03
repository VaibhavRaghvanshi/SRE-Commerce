import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom, timeout } from 'rxjs';
import { CommerceCartService } from './commerce-cart.service';
import { CommerceSessionService } from './commerce-session.service';
import { CommercePreparation } from '../models/commerce-cart.models';
import { CommerceOrder, CommerceOrderHistory, OrderContact } from '../models/commerce-order.models';
import { CommerceCheckout, CommerceCheckoutRequest, CommerceExchange, CommercePaymentMethods, CommerceReturn, CommerceShippingOption, CommerceTracking, SavedOrderReference } from '../models/commerce-checkout.models';
interface StoredOrder extends SavedOrderReference { key: string | null; }
interface PendingCheckout { signature: string; body: CommerceCheckoutRequest; reference: StoredOrder; headers: Record<string, string>; }
@Injectable({ providedIn: 'root' })
export class CommerceCheckoutService {
  private readonly http = inject(HttpClient); private readonly carts = inject(CommerceCartService); private readonly session = inject(CommerceSessionService);
  private readonly pending = new Map<string, PendingCheckout>(); private readonly memory = new Map<string, StoredOrder[]>();
  private readonly writes = new Map<string, { signature: string; id: string; body: unknown; url: string }>();
  private request<T>(url: string, method: 'GET' | 'POST', body?: unknown, headers: Record<string, string> = {}) { return firstValueFrom(this.http.request<T>(method, url, { body, headers }).pipe(timeout(15000))); }
  private storage(tenant: string) { return 'srecommerce.orders.v1.' + tenant; }
  private attemptKey(tenant: string, user: string | null) { return tenant + ':' + (user || 'guest'); }
  private guid(value: unknown): value is string { return typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value); }
  private read(tenant: string): StoredOrder[] {
    let entries = this.memory.get(tenant); if (entries) return entries;
    try { const value = typeof localStorage === 'undefined' ? null : localStorage.getItem(this.storage(tenant)); entries = value ? JSON.parse(value) : []; } catch { entries = []; }
    entries = Array.isArray(entries) ? entries.filter(r => r && typeof r === 'object' && this.guid(r.id) && typeof r.guest === 'boolean' && (r.userId === null || this.guid(r.userId)) && Number.isFinite(Date.parse(r.createdAt)) && Number.isInteger(r.state) && r.state >= 0 && r.state <= 6 && (r.guest ? typeof r.key === 'string' && /^[A-Za-z0-9_-]{43}$/.test(r.key) && r.userId === null : r.key === null && this.guid(r.userId))).slice(0, 20) : [];
    this.memory.set(tenant, entries); return entries;
  }
  private save(tenant: string, reference: StoredOrder) {
    // Store only opaque identifiers/capability and progress, never contact data, cart proof or OIDC tokens.
    const safe: StoredOrder = { id: reference.id, guest: reference.guest, userId: reference.userId, key: reference.key, createdAt: reference.createdAt, state: reference.state };
    const entries = [safe, ...this.read(tenant).filter(r => r.id !== safe.id)].slice(0, 20); this.memory.set(tenant, entries);
    try { if (typeof localStorage !== 'undefined') localStorage.setItem(this.storage(tenant), JSON.stringify(entries)); } catch { /* Browser memory still preserves the active retry. */ }
  }
  private forgetMissing(tenant: string, id: string) {
    const reference = this.read(tenant).find(r => r.id === id); const entries = this.read(tenant).filter(r => r.id !== id); this.memory.set(tenant, entries); if (reference) this.pending.delete(this.attemptKey(tenant, reference.userId));
    try { if (typeof localStorage !== 'undefined') localStorage.setItem(this.storage(tenant), JSON.stringify(entries)); } catch { /* Memory remains consistent. */ }
  }
  private available(reference: StoredOrder, tenant: string) { return reference.guest || !!this.session.token && this.session.access()?.selectedTenantId === tenant && this.session.access()?.userId === reference.userId; }
  references(tenant: string): SavedOrderReference[] { return this.read(tenant).filter(r => this.available(r, tenant)).map(r => ({ id: r.id, guest: r.guest, userId: r.userId, createdAt: r.createdAt, state: r.state })); }
  active(tenant: string, user?: string | null) { return this.references(tenant).find(r => ![2, 4, 6].includes(r.state) && (user === undefined || r.userId === user)) || null; }
  private reference(tenant: string, id: string): StoredOrder {
    const saved = this.read(tenant).find(r => r.id === id);
    if (saved && this.available(saved, tenant)) return saved;
    const access = this.session.access(); if (this.session.token && access?.selectedTenantId === tenant) return { id, guest: false, userId: access.userId, key: null, state: 2, createdAt: new Date().toISOString() };
    throw new Error('OrderProofUnavailable');
  }
  private base(tenant: string, guest: boolean) { return '/commerce/order/' + (guest ? 'public/' : '') + 'tenants/' + encodeURIComponent(tenant) + '/orders'; }
  private headers(reference: StoredOrder): Record<string, string> { return reference.guest ? { 'X-Order-Key': reference.key! } : {}; }
  methods(tenant: string) { return this.request<CommercePaymentMethods>('/commerce/payment/public/tenants/' + encodeURIComponent(tenant) + '/payments/methods', 'GET'); }
  regions(tenant: string) { return this.request<{ countries: string[]; currency: string }>('/commerce/shipping/public/tenants/' + encodeURIComponent(tenant) + '/shipping/regions', 'GET'); }
  options(tenant: string, preparation: CommercePreparation, country: string, postal: string, id: string) {
    const context = this.carts.checkoutContext(tenant, preparation);
    return this.request<CommerceShippingOption[]>('/commerce/shipping/' + (context.guest ? 'public/' : '') + 'tenants/' + encodeURIComponent(tenant) + '/shipping/quotes/options', 'POST',
      { id, preparationId: preparation.id, countryCode: country, postalCode: postal }, context.cartKey ? { 'X-Cart-Key': context.cartKey } : {});
  }
  async submit(tenant: string, preparation: CommercePreparation, shipping: CommerceShippingOption, method: string, contact: OrderContact) {
    const attemptKey = this.attemptKey(tenant, preparation.userId);
    const signature = JSON.stringify({ preparationId: preparation.id, shippingQuoteId: shipping.id, paymentMethod: method, contact }); let pending = this.pending.get(attemptKey);
    if (pending && !this.available(pending.reference, tenant)) throw new Error('CheckoutCustomerChanged');
    if (pending && signature !== pending.signature) throw new Error('CheckExistingOrderBeforeChangingCheckout');
    if (!pending) {
      if (shipping.preparationId !== preparation.id || Date.parse(shipping.expiresAt) <= Date.now()) throw new Error('ShippingQuoteExpired');
      if (this.active(tenant, preparation.userId)) throw new Error('CheckoutAlreadyPending');
      const context = this.carts.checkoutContext(tenant, preparation); const id = crypto.randomUUID();
      const bytes = context.guest ? crypto.getRandomValues(new Uint8Array(32)) : null;
      const key = bytes ? btoa(String.fromCharCode(...bytes)).replace(/=+$/, '').split('+').join('-').split('/').join('_') : null;
      const reference: StoredOrder = { id, guest: context.guest, userId: context.guest ? null : this.session.access()!.userId, key, createdAt: new Date().toISOString(), state: 0 };
      pending = { signature, reference, body: { id, preparationId: preparation.id, shippingQuoteId: shipping.id, paymentMethod: method, contact: { ...contact } }, headers: { ...this.headers(reference), ...(context.cartKey ? { 'X-Cart-Key': context.cartKey } : {}) } };
      this.pending.set(attemptKey, pending); this.save(tenant, reference);
    }
    try {
      const result = await this.request<CommerceCheckout>(this.base(tenant, pending.reference.guest) + '/checkout', 'POST', pending.body, pending.headers);
      this.observe(tenant, pending.reference, result); return result;
    } catch (error) {
      if (error instanceof HttpErrorResponse && [400, 403, 409].includes(error.status)) {
        try { await this.status(tenant, pending.reference.id); } catch (probe) {
          if (probe instanceof HttpErrorResponse && probe.status === 404 && probe.error?.title === 'CheckoutNotFound') this.forgetMissing(tenant, pending.reference.id);
        }
      }
      throw error;
    }
  }
  private observe(tenant: string, reference: StoredOrder, checkout: CommerceCheckout) {
    this.save(tenant, { ...reference, state: checkout.state });
    if ([2, 4, 6].includes(checkout.state)) this.pending.delete(this.attemptKey(tenant, reference.userId));
  }
  async status(tenant: string, id: string) { const reference = this.reference(tenant, id); const checkout = await this.request<CommerceCheckout>(this.base(tenant, reference.guest) + '/checkouts/' + encodeURIComponent(id), 'GET', undefined, this.headers(reference)); this.observe(tenant, reference, checkout); return checkout; }
  order(tenant: string, id: string) { const r = this.reference(tenant, id); return this.request<CommerceOrder>(this.base(tenant, r.guest) + '/' + encodeURIComponent(id), 'GET', undefined, this.headers(r)); }
  history(tenant: string, id: string) { const r = this.reference(tenant, id); return this.request<CommerceOrderHistory[]>(this.base(tenant, r.guest) + '/' + encodeURIComponent(id) + '/history', 'GET', undefined, this.headers(r)); }
  returns(tenant: string, id: string) { const r = this.reference(tenant, id); return this.request<CommerceReturn[]>(this.base(tenant, r.guest) + '/' + encodeURIComponent(id) + '/returns', 'GET', undefined, this.headers(r)); }
  exchanges(tenant: string, id: string) { const r = this.reference(tenant, id); return this.request<CommerceExchange[]>(this.base(tenant, r.guest) + '/' + encodeURIComponent(id) + '/exchanges', 'GET', undefined, this.headers(r)); }
  tracking(tenant: string, id: string) { const r = this.reference(tenant, id); return this.request<CommerceTracking[]>('/commerce/shipping/' + (r.guest ? 'public/' : '') + 'tenants/' + encodeURIComponent(tenant) + '/shipping/orders/' + encodeURIComponent(id) + '/tracking', 'GET', undefined, this.headers(r)); }
  list(tenant: string, page: number) { return this.request<{ items: CommerceOrder[]; total: number; page: number; pageSize: number }>(this.base(tenant, false) + '?page=' + page, 'GET'); }
  private uncertain(error: unknown) { return !(error instanceof HttpErrorResponse) || error.status === 0 || error.status >= 500 || error.status === 429; }
  private async mutation<T>(tenant: string, order: CommerceOrder, action: 'cancel' | 'returns' | 'exchanges', value: object, idField: 'id' | 'operationId') {
    const reference = this.reference(tenant, order.id); const label = tenant + ':' + order.id + ':' + action; const signature = JSON.stringify({ version: order.version, ...value }); let write = this.writes.get(label);
    if (write && write.signature !== signature) throw new Error('RetryPreviousOrderActionFirst');
    if (!write) { const id = crypto.randomUUID(); write = { signature, id, body: { [idField]: id, version: order.version, ...value }, url: this.base(tenant, reference.guest) + '/' + order.id + '/' + action }; this.writes.set(label, write); }
    try { const result = await this.request<T>(write.url, 'POST', write.body, this.headers(reference)); this.writes.delete(label); return result; } catch (error) { if (!this.uncertain(error)) this.writes.delete(label); throw error; }
  }
  cancel(tenant: string, order: CommerceOrder, reason: string) { return this.mutation<CommerceOrder>(tenant, order, 'cancel', { reason: reason.trim() }, 'operationId'); }
  return(tenant: string, order: CommerceOrder, variantId: string, quantity: number, reason: string) { return this.mutation<CommerceReturn>(tenant, order, 'returns', { items: [{ variantId, quantity }], reason: reason.trim() }, 'id'); }
  exchange(tenant: string, order: CommerceOrder, originalVariantId: string, replacementVariantId: string, quantity: number, reason: string) { return this.mutation<{ id: string; status: number }>(tenant, order, 'exchanges', { originalVariantId, replacementVariantId, quantity, reason: reason.trim() }, 'id'); }
}
