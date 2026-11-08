import { CommonModule } from '@angular/common';
import { Component, DestroyRef, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom, timeout } from 'rxjs';
import { CommerceCartService } from '../../../core/services/commerce-cart.service';
import { CommerceCheckoutService } from '../../../core/services/commerce-checkout.service';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
import { CommercePreparation } from '../../../core/models/commerce-cart.models';
import { CommerceCheckout, CommerceShippingOption } from '../../../core/models/commerce-checkout.models';
import { CommerceOrder, OrderContact } from '../../../core/models/commerce-order.models';

@Component({
  selector: 'app-counter-pos',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './pos.component.html',
  styleUrl: './pos.component.scss'
})
export class PosComponent {
  readonly tenant = inject(ActivatedRoute).snapshot.paramMap.get('tenant') || '';
  readonly session = inject(CommerceSessionService);
  private readonly carts = inject(CommerceCartService);
  private readonly checkout = inject(CommerceCheckoutService);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);
  private readonly scanField = viewChild<ElementRef<HTMLInputElement>>('scanField');
  readonly cartLines = computed(() => this.carts.view(this.tenant)?.items ?? []);
  readonly countries = signal<string[]>([]);
  readonly preparation = signal<CommercePreparation | null>(null);
  readonly choices = signal<CommerceShippingOption[]>([]);
  readonly selected = signal('');
  readonly pickup = computed(() => this.choices().find(quote => quote.id === this.selected()) || null);
  readonly total = computed(() => {
    const quote = this.preparation()?.quote;
    return Number(((quote?.calculation.total || 0) + (this.pickup()?.total || 0)).toFixed(quote?.decimalPlaces ?? 2));
  });
  readonly progress = signal<CommerceCheckout | null>(null);
  readonly reference = signal<string | null>(null);
  readonly busy = signal(false);
  readonly message = signal('');
  readonly error = signal('');
  scanCode = '';
  cash: number | null = null;
  buyer = '';
  email = '';
  address = '';
  city = '';
  country = '';
  postal = '';
  private timer: ReturnType<typeof setTimeout> | null = null;
  private stopped = false;
  private polls = 0;
  private handoverRequest: { orderId: string; operationId: string; version: string } | null = null;

  get canOperate() {
    const actor = this.session.access();
    return !!this.session.token && actor?.selectedTenantId === this.tenant && !actor.isPlatformAdmin &&
      ['Owner', 'Manager', 'Staff'].includes(actor.tenantRole || '');
  }

  constructor() {
    this.destroy.onDestroy(() => { this.stopped = true; if (this.timer) clearTimeout(this.timer); });
    if (typeof window !== 'undefined' && this.canOperate) void this.initialize();
  }

  async signIn() {
    try { await this.session.signIn(this.tenant, 'pos'); }
    catch { this.error.set('Counter sign-in could not start.'); }
  }

  async initialize() {
    const active = this.checkout.active(this.tenant, this.session.access()?.userId)?.id;
    const saved = this.route.snapshot.queryParamMap.get('order');
    if (saved || active) { await this.check(saved || active!); return; }
    await this.work(async () => {
      const [regions] = await Promise.all([this.checkout.regions(this.tenant), this.carts.load(this.tenant)]);
      this.countries.set(regions.countries);
      if (regions.countries.length === 1) this.country = regions.countries[0];
    }, 'Counter setup could not be loaded. Confirm that Cart and Shipping are enabled, then retry.');
  }

  private async work(action: () => Promise<void>, failure: string) {
    if (!this.canOperate || this.busy()) return;
    this.busy.set(true); this.message.set(''); this.error.set('');
    try { await action(); }
    catch { this.error.set(failure); }
    finally { this.busy.set(false); }
  }

  clearQuote() {
    this.preparation.set(null); this.choices.set([]); this.selected.set(''); this.cash = null;
  }

  async scan() {
    const code = this.scanCode.trim();
    if (!code || this.reference()) return;
    await this.work(async () => {
      const url = '/commerce/catalog/tenants/' + encodeURIComponent(this.tenant) + '/catalog/counter/lookup';
      const item = await firstValueFrom(this.http.get<{ variantId: string; name: string }>(url, { params: { code } }).pipe(timeout(15000)));
      await this.carts.add(this.tenant, item.variantId, 1);
      this.clearQuote(); this.scanCode = ''; this.message.set(item.name + ' added.');
    }, 'The item could not be added. Check the exact published SKU or barcode and retry the same scan.');
    this.scanField()?.nativeElement.focus();
  }

  remove(variant: string) {
    return this.work(async () => { await this.carts.remove(this.tenant, variant); this.clearQuote(); },
      'The item change could not be confirmed. Retry the same removal.');
  }

  quote() {
    if (!this.country || !this.postal.trim() || !this.cartLines().length) return;
    return this.work(async () => {
      this.clearQuote();
      const prepared = await this.carts.prepare(this.tenant, '');
      const options = await this.checkout.options(this.tenant, prepared, this.country, this.postal.trim().toUpperCase(), crypto.randomUUID());
      const pickup = options.filter(option => option.kind === 1);
      this.preparation.set(prepared); this.choices.set(pickup);
      if (pickup.length === 1) this.selected.set(pickup[0].id);
      if (!pickup.length) this.error.set('No pickup branch is available for this location and basket. Check the Shipping workspace.');
    }, 'The server could not quote this basket. Check current stock and the pickup location.');
  }

  async sale() {
    const prepared = this.preparation(), pickup = this.pickup();
    if (!prepared || !pickup || this.reference() || this.cash !== this.total()) return;
    const contact: OrderContact = { fullName: this.buyer.trim(), email: this.email.trim(), phone: null,
      addressLine1: this.address.trim(), addressLine2: null, city: this.city.trim(), countryCode: this.country, postalCode: this.postal.trim().toUpperCase() };
    if (!contact.fullName || !contact.addressLine1 || !contact.city) return;
    await this.work(async () => {
      const sale = await this.checkout.submit(this.tenant, prepared, pickup, 'CounterCash', contact, { counterCashReceived: this.total() });
      this.observe(sale);
    }, 'The counter sale was not confirmed. Check the saved reference before recording another payment.');
    const active = this.checkout.active(this.tenant, this.session.access()?.userId)?.id;
    if (!this.reference() && active) await this.check(active);
  }

  private observe(value: CommerceCheckout) {
    this.reference.set(value.id); this.progress.set(value);
    void this.router.navigate([], { relativeTo: this.route, queryParams: { order: value.id }, replaceUrl: true });
    if (this.timer) clearTimeout(this.timer);
    if (!this.stopped && [0, 1, 3].includes(value.state) && this.polls++ < 30)
      this.timer = setTimeout(() => void this.check(value.id), 1800);
  }

  async check(id = this.reference() || '') {
    if (!id) return;
    this.reference.set(id);
    await this.work(async () => this.observe(await this.checkout.status(this.tenant, id)),
      'Could not check the order. Retry this reference before recording another sale.');
  }

  handover(order: CommerceOrder) {
    return this.work(async () => {
      if (this.handoverRequest?.orderId !== order.id)
        this.handoverRequest = { orderId: order.id, operationId: crypto.randomUUID(), version: order.version };
      const operation = this.handoverRequest;
      const path = '/commerce/order/tenants/' + encodeURIComponent(this.tenant) + '/order-management/orders/' + encodeURIComponent(order.id) + '/counter-handover';
      try {
        const updated = await firstValueFrom(this.http.post<CommerceOrder>(path, { operationId: operation.operationId, version: operation.version }).pipe(timeout(15000)));
        this.progress.update(value => value && { ...value, order: updated });
        this.handoverRequest = null;
      } catch (error) {
        if (error instanceof HttpErrorResponse && [400, 403, 404, 409].includes(error.status)) this.handoverRequest = null;
        throw error;
      }
    }, 'Handover could not be confirmed. Check the order status before retrying.');
  }

  printReceipt() { window.print(); }

  async nextSale() {
    const current = this.progress();
    if (this.busy() || !current || !([4, 6].includes(current.state) || current.state === 2 && current.order?.status === 5)) return;
    this.reference.set(null); this.progress.set(null); this.polls = 0; this.clearQuote(); this.buyer = ''; this.email = '';
    await this.router.navigate([], { relativeTo: this.route, queryParams: { order: null }, replaceUrl: true });
    await this.initialize();
    this.scanField()?.nativeElement.focus();
  }
}
