import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommerceCatalogService } from '../../../core/services/commerce-catalog.service';
import { CommerceCartService } from '../../../core/services/commerce-cart.service';
import { CommerceCheckoutService } from '../../../core/services/commerce-checkout.service';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
import { CommerceTenant } from '../../../core/models/commerce.models';
import { CommercePreparation } from '../../../core/models/commerce-cart.models';
import { CommerceCheckout, CommerceShippingOption } from '../../../core/models/commerce-checkout.models';
import { OrderContact } from '../../../core/models/commerce-order.models';
import { CustomerAddress } from '../../../core/models/commerce-customer.models';
import { CommerceCustomerService } from '../../../core/services/commerce-customer.service';

@Component({ selector: 'app-commerce-checkout', standalone: true, imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './checkout.component.html', styleUrl: './checkout.component.scss' })
export class CommerceCheckoutComponent {
  readonly session = inject(CommerceSessionService); private readonly catalog = inject(CommerceCatalogService); private readonly carts = inject(CommerceCartService);
  private readonly api = inject(CommerceCheckoutService); private readonly route = inject(ActivatedRoute); private readonly router = inject(Router); private readonly destroy = inject(DestroyRef); private readonly fb = inject(FormBuilder).nonNullable;
  readonly tenant = signal<CommerceTenant | null>(null); readonly preparation = signal<CommercePreparation | null>(null); readonly options = signal<CommerceShippingOption[]>([]);
  readonly countries = signal<string[]>([]); readonly methods = signal<string[]>([]); readonly sandbox = signal(true); readonly selected = signal(''); readonly busy = signal(true);
  readonly error = signal(''); readonly status = signal<CommerceCheckout | null>(null); readonly needsSignIn = signal(false); readonly waiting = signal(false);
  readonly reference = signal<string | null>(null);
  private readonly customer = inject(CommerceCustomerService);
  readonly addresses = signal<CustomerAddress[]>([]);
  readonly addressError = signal('');
  readonly shipping = computed(() => this.options().find(o => o.id === this.selected()) || null);
  readonly total = computed(() => Number(((this.preparation()?.quote.calculation.total || 0) + (this.shipping()?.total || 0)).toFixed(this.preparation()?.quote.decimalPlaces ?? 2)));
  readonly form = this.fb.group({ fullName: ['', [Validators.required, Validators.maxLength(160), Validators.pattern(/^[^<>\x00-\x1f]+$/)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]], phone: ['', [Validators.maxLength(25), Validators.pattern(/^[0-9+ -]*$/)]],
    addressLine1: ['', [Validators.required, Validators.maxLength(200), Validators.pattern(/^[^<>\x00-\x1f]+$/)]], addressLine2: ['', [Validators.maxLength(200), Validators.pattern(/^[^<>\x00-\x1f]*$/)]],
    city: ['', [Validators.required, Validators.maxLength(100), Validators.pattern(/^[^<>\x00-\x1f]+$/)]], countryCode: ['', Validators.required],
    postalCode: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(16), Validators.pattern(/^[A-Za-z0-9 -]+$/)]], paymentMethod: ['', Validators.required] });
  private timer: ReturnType<typeof setTimeout> | null = null; private destroyed = false; private polls = 0; private statusRead = 0;
  constructor() {
    effect(() => { const tenant = this.tenant(); if (!tenant || !this.customer.signedIn(tenant.tenantId)) this.addresses.set([]); });
    this.destroy.onDestroy(() => { this.destroyed = true; if (this.timer) clearTimeout(this.timer); });
    for (const control of [this.form.controls.countryCode, this.form.controls.postalCode]) control.valueChanges.pipe(takeUntilDestroyed(this.destroy)).subscribe(() => { this.options.set([]); this.selected.set(''); });
    if (typeof window !== 'undefined') void this.initialize();
  }
  countryName(code: string) { try { return new Intl.DisplayNames(['en'], { type: 'region' }).of(code) || code; } catch { return code; } }
  private async initialize() {
    try {
      const tenant = await firstValueFrom(this.catalog.tenant(this.route.snapshot.paramMap.get('slug') || '')); this.tenant.set(tenant);
      const existing = this.route.snapshot.queryParamMap.get('order') || this.api.active(tenant.tenantId)?.id;
      if (existing) { await this.check(existing); return; }
      if (!['cart', 'orders', 'payments', 'shipping'].every(m => tenant.modules.includes(m))) throw new Error('CheckoutUnavailable');
      const [regions, methods] = await Promise.all([this.api.regions(tenant.tenantId), this.api.methods(tenant.tenantId)]);
      this.countries.set(regions.countries); this.methods.set(methods.methods); this.sandbox.set(methods.sandbox);
      this.form.patchValue({ countryCode: regions.countries.length === 1 ? regions.countries[0] : '', paymentMethod: methods.methods[0] || '', email: this.session.user()?.profile.email || '' });
      if (this.customer.signedIn(tenant.tenantId)) {
        try { const user = this.session.access()?.userId; const addresses = await this.customer.addresses(tenant.tenantId);
          if (this.customer.signedIn(tenant.tenantId) && this.session.access()?.userId === user) this.addresses.set(addresses); }
        catch { this.addressError.set('Saved addresses could not be loaded. You can enter the delivery details below.'); }
      }
      await this.refreshPreparation();
    } catch { this.error.set('Checkout is temporarily unavailable. Your saved cart is safe. Please try again.'); } finally { this.busy.set(false); }
  }
  private async refreshPreparation() {
    const tenant = this.tenant(); if (!tenant) return;
    const cart = await this.carts.load(tenant.tenantId); if (!cart.items.length) throw new Error('CartEmpty');
    this.preparation.set(await this.carts.prepare(tenant.tenantId, this.route.snapshot.queryParamMap.get('coupon') || '')); this.options.set([]); this.selected.set('');
  }
  useAddress(id: string) {
    const address = this.addresses().find(row => row.id === id); if (!address) return;
    const { fullName, email, phone, addressLine1, addressLine2, city, countryCode, postalCode } = address;
    this.form.patchValue({ fullName, email, phone: phone || '', addressLine1, addressLine2: addressLine2 || '', city,
      countryCode: this.countries().includes(countryCode) ? countryCode : '', postalCode });
    this.options.set([]); this.selected.set('');
  }
  async delivery() {
    const tenant = this.tenant(); if (!tenant || this.busy() || this.status()) return;
    this.form.controls.countryCode.markAsTouched(); this.form.controls.postalCode.markAsTouched();
    if (this.form.controls.countryCode.invalid || this.form.controls.postalCode.invalid) return;
    this.busy.set(true); this.error.set('');
    try {
      if (!this.preparation() || Date.parse(this.preparation()!.expiresAt) <= Date.now()) await this.refreshPreparation();
      const options = await this.api.options(tenant.tenantId, this.preparation()!, this.form.controls.countryCode.value, this.form.controls.postalCode.value.trim().toUpperCase(), crypto.randomUUID());
      this.options.set(options); this.selected.set(options[0]?.id || ''); if (!options.length) this.error.set('No delivery method is available for this address and order. Try another postal area or contact the store.');
    } catch { this.error.set('Delivery options could not be checked. Refresh your cart if its prices or quantities changed.'); } finally { this.busy.set(false); }
  }
  async submit() {
    const tenant = this.tenant(), preparation = this.preparation(), shipping = this.shipping(); this.form.markAllAsTouched();
    if (!tenant || !preparation || !shipping || this.busy() || this.form.invalid || this.status()) return;
    this.busy.set(true); this.error.set(''); const value = this.form.getRawValue();
    const contact: OrderContact = { fullName: value.fullName.trim(), email: value.email.trim(), phone: value.phone.trim() || null, addressLine1: value.addressLine1.trim(), addressLine2: value.addressLine2.trim() || null,
      city: value.city.trim(), countryCode: value.countryCode.toUpperCase(), postalCode: value.postalCode.trim().toUpperCase() };
    try {
      const result = await this.api.submit(tenant.tenantId, preparation, shipping, value.paymentMethod, contact); this.status.set(result);
      void this.router.navigate([], { relativeTo: this.route, queryParams: { order: result.id }, queryParamsHandling: 'merge', replaceUrl: true }); this.polls = 0; this.schedule(result);
    } catch {
      const pending = this.api.active(tenant.tenantId); if (pending) { this.error.set('The response was not confirmed. Check this checkout before submitting another order.'); await this.check(pending.id); }
      else this.error.set('Checkout could not be started. Check your details and refresh expired delivery options before retrying.');
    } finally { this.busy.set(false); }
  }
  private schedule(result: CommerceCheckout) {
    if (this.timer) clearTimeout(this.timer); this.waiting.set(![2, 4, 5, 6].includes(result.state));
    if (!this.waiting() || this.destroyed || this.polls >= 30) return;
    this.timer = setTimeout(() => { this.polls++; void this.check(result.id); }, 1500);
  }
  async check(id?: string) {
    const tenant = this.tenant(); const reference = id || this.status()?.id || this.reference() || (tenant ? this.api.active(tenant.tenantId)?.id : null); if (!tenant || !reference) return;
    this.reference.set(reference); const sequence = ++this.statusRead;
    try { const result = await this.api.status(tenant.tenantId, reference); if (this.destroyed || sequence !== this.statusRead) return; this.status.set(result); this.needsSignIn.set(false); this.error.set(''); this.schedule(result); }
    catch { if (this.destroyed || sequence !== this.statusRead) return; this.waiting.set(false); const guest = this.api.references(tenant.tenantId).some(r => r.id === reference && r.guest); this.needsSignIn.set(!guest && !this.session.token); this.error.set(guest ? 'The latest checkout status could not be loaded. Check this reference again before placing another order.' : 'The latest checkout status could not be loaded. Sign in if this is an account order, then check again.'); }
  }
  async signIn() { const tenant = this.tenant(); if (tenant) try { await this.session.signInStore(tenant.tenantId, tenant.slug, 'orders'); } catch { this.error.set('Sign-in could not be started.'); } }
  async cancelPending(id: string) {
    const tenant = this.tenant(), current = this.status();
    if (!tenant || !current || current.id !== id || ![0,1].includes(current.state) || this.busy()) return;
    if (this.timer) clearTimeout(this.timer);
    this.statusRead++; this.busy.set(true); this.error.set('');
    try { const updated = await this.api.cancelPending(tenant.tenantId, id, 'Customer requested checkout cancellation'); this.status.set(updated); this.polls = 0; this.schedule(updated); }
    catch { this.error.set('Cancellation could not be confirmed. Check the current status and retry if checkout is still pending.'); }
    finally { this.busy.set(false); }
  }
}
