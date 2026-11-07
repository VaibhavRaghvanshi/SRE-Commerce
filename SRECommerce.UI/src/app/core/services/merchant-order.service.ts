import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { timeout } from 'rxjs';
import { CommerceOrder, CommerceOrderHistory, CommerceOrderPage, CommerceOrderPolicy } from '../models/commerce-order.models';
import { CheckoutReview, CommerceCheckout } from '../models/commerce-checkout.models';
import { GstWorksheet, GstWorksheetRequest } from '../models/commerce-gst.models';

@Injectable({ providedIn: 'root' })
export class MerchantOrderService {
  private readonly http = inject(HttpClient);
  private base(tenant: string) { return '/commerce/order/tenants/' + encodeURIComponent(tenant) + '/order-management'; }
  private options(reason: string) { return { headers: new HttpHeaders(reason.trim() ? { 'X-Support-Reason': reason.trim() } : {}) }; }
  list(tenant: string, page: number, status: number | null, reason: string) {
    return this.http.get<CommerceOrderPage>(this.base(tenant) + '/orders', {
      ...this.options(reason), params: status === null ? { page } : { page, status }
    }).pipe(timeout(15000));
  }
  order(tenant: string, id: string, reason: string) {
    return this.http.get<CommerceOrder>(this.base(tenant) + '/orders/' + encodeURIComponent(id), this.options(reason)).pipe(timeout(15000));
  }
  history(tenant: string, id: string, reason: string) {
    return this.http.get<CommerceOrderHistory[]>(this.base(tenant) + '/orders/' + encodeURIComponent(id) + '/history', this.options(reason)).pipe(timeout(15000));
  }
  reviews(tenant: string, page: number, reason: string) {
    return this.http.get<CheckoutReview[]>(this.base(tenant) + '/checkout-reviews', { ...this.options(reason), params: { page } }).pipe(timeout(15000));
  }
  recover(tenant: string, review: CheckoutReview, cancel: boolean, note: string, reason: string) {
    return this.http.post<CommerceCheckout>(this.base(tenant) + '/checkout-reviews/' + encodeURIComponent(review.id) + '/recover',
      { version: review.version, cancel, reason: note.trim() }, this.options(reason)).pipe(timeout(15000));
  }
  policy(tenant: string, reason: string) {
    return this.http.get<CommerceOrderPolicy>(this.base(tenant) + '/policy', this.options(reason)).pipe(timeout(15000));
  }
  savePolicy(tenant: string, value: CommerceOrderPolicy, reason: string) {
    return this.http.put<CommerceOrderPolicy>(this.base(tenant) + '/policy', value, this.options(reason)).pipe(timeout(15000));
  }
  cancel(tenant: string, order: CommerceOrder, operationId: string, note: string, reason: string) {
    return this.http.post<CommerceOrder>(this.base(tenant) + '/orders/' + encodeURIComponent(order.id) + '/cancel',
      { operationId, version: order.version, reason: note.trim() }, this.options(reason)).pipe(timeout(15000));
  }
  invoice(tenant: string, order: CommerceOrder, operationId: string, reference: string, reason: string) {
    return this.http.post<CommerceOrder>(this.base(tenant) + '/orders/' + encodeURIComponent(order.id) + '/invoice-reference',
      { operationId, version: order.version, reference: reference.trim() }, this.options(reason)).pipe(timeout(15000));
  }
  gstWorksheet(tenant: string, orderId: string, input: GstWorksheetRequest, reason: string) {
    return this.http.post<GstWorksheet>(this.base(tenant) + '/orders/' + encodeURIComponent(orderId) + '/gst-worksheet', input,
      this.options(reason)).pipe(timeout(15000));
  }
}
