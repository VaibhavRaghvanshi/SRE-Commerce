import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CommerceCatalogService } from '../../../core/services/commerce-catalog.service';
import { CommerceCustomerService } from '../../../core/services/commerce-customer.service';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
import { CustomerAddress, SavedProduct } from '../../../core/models/commerce-customer.models';
import { CommerceTenant } from '../../../core/models/commerce.models';
import { StoreAppearanceDirective } from '../presentation/store-appearance.directive';
import { StoreLanguageService } from '../../../core/services/store-language.service';
import { StoreLanguageComponent } from '../presentation/store-language.component';

@Component({ selector: 'app-commerce-customer-account', standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, StoreAppearanceDirective, StoreLanguageComponent],
  templateUrl: './customer-account.component.html', styleUrl: './customer-account.component.scss' })
export class CommerceCustomerAccountComponent {
  readonly locale=inject(StoreLanguageService);
  readonly api = inject(CommerceCustomerService);
  private readonly catalog = inject(CommerceCatalogService);
  private readonly session = inject(CommerceSessionService);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder).nonNullable;
  readonly tenant = signal<CommerceTenant | null>(null);
  readonly addresses = signal<CustomerAddress[]>([]);
  readonly wishlist = signal<SavedProduct[]>([]);
  readonly recent = signal<SavedProduct[]>([]);
  readonly busy = signal(false); readonly error = signal(''); readonly message = signal('');
  readonly hasMore = signal(false); readonly signedIn = computed(() => !!this.tenant() && this.api.signedIn(this.tenant()!.tenantId));
  readonly editing = signal(false); private addressId = ''; private addressVersion: string | null = null;
  page = 1;
  readonly form = this.fb.group({ label: ['', [Validators.required, Validators.maxLength(40)]], fullName: ['', [Validators.required, Validators.maxLength(160)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]], phone: ['', [Validators.maxLength(25), Validators.pattern(/^[0-9+ -]*$/)]],
    addressLine1: ['', [Validators.required, Validators.maxLength(200)]], addressLine2: ['', Validators.maxLength(200)],
    city: ['', [Validators.required, Validators.maxLength(100)]], countryCode: ['', [Validators.required, Validators.pattern(/^[A-Za-z]{2}$/)]],
    postalCode: ['', [Validators.required, Validators.pattern(/^[A-Za-z0-9][A-Za-z0-9 -]{1,15}$/)]] });

  constructor() {
    effect(() => {
      if (!this.session.token || this.session.access()?.selectedTenantId !== this.tenant()?.tenantId) {
        this.addresses.set([]); this.wishlist.set([]); this.recent.set([]); this.editing.set(false); this.form.reset();
      }
    });
    if (typeof window !== 'undefined') void this.load();
  }
  private async run(action: () => Promise<void>) {
    if (this.busy()) return; this.busy.set(true); this.error.set(''); this.message.set('');
    try { await action(); }
    catch (e) { this.error.set(e instanceof HttpErrorResponse && e.status === 409 ? 'These details changed. Refresh before saving again.' : 'Your account details could not be confirmed. Refresh or sign in again.'); }
    finally { this.busy.set(false); }
  }
  load() { return this.run(async () => {
    const user = this.session.access()?.userId;
    const tenant = this.tenant() || await firstValueFrom(this.catalog.tenant(this.route.snapshot.paramMap.get('slug') || '')); this.tenant.set(tenant);
    if (!tenant.modules.includes('cart')) throw new Error('AccountUnavailable');
    const recent = await this.api.recent(tenant.tenantId);
    if (this.session.access()?.userId !== user) return;
    this.recent.set(recent);
    if (this.api.signedIn(tenant.tenantId)) {
      const [addresses, wishlist] = await Promise.all([this.api.addresses(tenant.tenantId), this.api.saved(tenant.tenantId, 0, this.page)]);
      if (this.session.access()?.userId !== user || !this.api.signedIn(tenant.tenantId)) return;
      this.addresses.set(addresses); this.wishlist.set(wishlist.items); this.hasMore.set(wishlist.hasMore);
    } else { this.addresses.set([]); this.wishlist.set([]); this.hasMore.set(false); }
  }); }
  async signIn() { const t = this.tenant(); if (t) try { await this.session.signInStore(t.tenantId, t.slug, 'account'); } catch { this.error.set('Sign-in could not be started.'); } }
  edit(row?: CustomerAddress) {
    this.addressId = row?.id || crypto.randomUUID(); this.addressVersion = row?.version || null;
    this.form.reset({ label: row?.label || '', fullName: row?.fullName || '', email: row?.email || this.session.user()?.profile.email || '',
      phone: row?.phone || '', addressLine1: row?.addressLine1 || '', addressLine2: row?.addressLine2 || '', city: row?.city || '', countryCode: row?.countryCode || '', postalCode: row?.postalCode || '' });
    this.editing.set(true);
  }
  save() {
    this.form.markAllAsTouched(); if (this.form.invalid || !this.signedIn()) return;
    return this.run(async () => {
      const t = this.tenant()!, value = this.form.getRawValue();
      const user = this.session.access()?.userId;
      const saved = await this.api.saveAddress(t.tenantId, this.addressId, { ...value, phone: value.phone || null, addressLine2: value.addressLine2 || null, version: this.addressVersion });
      if (this.session.access()?.userId !== user || !this.api.signedIn(t.tenantId)) return;
      this.addresses.update(rows => [saved, ...rows.filter(row => row.id !== saved.id)]); this.editing.set(false); this.message.set('Address saved.');
    });
  }
  archive(row: CustomerAddress) { return this.run(async () => {
    await this.api.archiveAddress(this.tenant()!.tenantId, row); this.addresses.update(rows => rows.filter(a => a.id !== row.id));
    if (this.addressId === row.id) this.editing.set(false); this.message.set('Address removed from your address book.');
  }); }
  remove(row: SavedProduct) { return this.run(async () => {
    await this.api.wishlist(this.tenant()!.tenantId, row.productId, false, row.version); this.wishlist.update(rows => rows.filter(p => p.productId !== row.productId));
    this.message.set('Product removed from your wishlist.');
  }); }
  clearRecent() { return this.run(async () => { await this.api.clearRecent(this.tenant()!.tenantId); this.recent.set([]); this.message.set('Recently viewed products cleared.'); }); }
}
