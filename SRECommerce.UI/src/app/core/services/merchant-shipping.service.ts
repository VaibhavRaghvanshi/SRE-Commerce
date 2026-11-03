import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { timeout } from 'rxjs';
export interface ShippingZone { id: string; code: string; name: string; countries: string[]; postalPrefixes: string[]; active: boolean; version: string; }
export interface ShippingMethod { id: string; zoneId: string; code: string; name: string; description: string; kind: number; currency: string; flatFee: number; perUnitFee: number; taxRate: number; freeAbove: number | null; minimumOrder: number; maximumOrder: number | null; maximumUnits: number; pickupWarehouseId: string | null; active: boolean; version: string; }
export interface ShippingShipment { id: string; orderId: string; warehouseId: string; kind: number; provider: string; courierReference: string; status: number; items: { variantId: string; quantity: number }[]; createdAt: string; dispatchedAt: string | null; deliveredAt: string | null; version: string; }
export interface ShippingPickup { id: string; returnId: string; orderId: string; warehouseId: string; windowFrom: string; windowTo: string; status: number; note: string; version: string; }
export interface ShippingHistory { shipment: ShippingShipment; contact: { fullName: string; email: string; phone: string | null; addressLine1: string; addressLine2: string | null; city: string; countryCode: string; postalCode: string }; events: { sequence: number; status: number; note: string; occurredAt: string }[]; callbacks: { id: string; targetStatus: number; sent: boolean; attempts: number; manualReview: boolean; lastFault: string | null; nextAttemptAt: string }[]; }
@Injectable({ providedIn: 'root' })
export class MerchantShippingService {
  private readonly http = inject(HttpClient);
  private base(tenant: string) { return '/commerce/shipping/tenants/' + encodeURIComponent(tenant) + '/shipping'; }
  private options(reason: string) { return { headers: new HttpHeaders(reason.trim() ? { 'X-Support-Reason': reason.trim() } : {}) }; }
  lookups(tenant: string, reason: string) { return this.http.get<{ zones: ShippingZone[]; methods: ShippingMethod[]; currency: string }>(this.base(tenant) + '/lookups', this.options(reason)).pipe(timeout(15000)); }
  shipments(tenant: string, page: number, status: number | null, reason: string) { return this.http.get<{ items: ShippingShipment[]; total: number; page: number; pageSize: number }>(this.base(tenant) + '/shipments', { ...this.options(reason), params: status === null ? { page } : { page, status } }).pipe(timeout(15000)); }
  pickups(tenant: string, reason: string) { return this.http.get<ShippingPickup[]>(this.base(tenant) + '/return-pickups', this.options(reason)).pipe(timeout(15000)); }
  history(tenant: string, id: string, reason: string) { return this.http.get<ShippingHistory>(this.base(tenant) + '/shipments/' + encodeURIComponent(id) + '/history', this.options(reason)).pipe(timeout(15000)); }
  saveZone(tenant: string, id: string | null, body: unknown, reason: string) { const url = this.base(tenant) + '/zones' + (id ? '/' + encodeURIComponent(id) : ''); return this.http.request<ShippingZone>(id ? 'PUT' : 'POST', url, { ...this.options(reason), body }).pipe(timeout(15000)); }
  saveMethod(tenant: string, id: string | null, body: unknown, reason: string) { const url = this.base(tenant) + '/methods' + (id ? '/' + encodeURIComponent(id) : ''); return this.http.request<ShippingMethod>(id ? 'PUT' : 'POST', url, { ...this.options(reason), body }).pipe(timeout(15000)); }
  state(tenant: string, id: string, body: unknown, reason: string) { return this.http.post<ShippingShipment>(this.base(tenant) + '/shipments/' + encodeURIComponent(id) + '/status', body, this.options(reason)).pipe(timeout(15000)); }
  retry(tenant: string, id: string, reason: string) { return this.http.post(this.base(tenant) + '/shipments/callbacks/' + encodeURIComponent(id) + '/retry', {}, this.options(reason)).pipe(timeout(15000)); }
  schedule(tenant: string, body: unknown, reason: string) { return this.http.post<ShippingPickup>(this.base(tenant) + '/return-pickups', body, this.options(reason)).pipe(timeout(15000)); }
  pickupState(tenant: string, id: string, body: unknown, reason: string) { return this.http.post<ShippingPickup>(this.base(tenant) + '/return-pickups/' + encodeURIComponent(id) + '/status', body, this.options(reason)).pipe(timeout(15000)); }
}
