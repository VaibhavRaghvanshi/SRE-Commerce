import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { timeout } from 'rxjs';
export interface MerchantPayment { id: string; orderId: string; currency: string; amount: number; method: string; status: number; refunded: number; version: string; provider: string; providerReference: string; sandbox: boolean; }
export interface PaymentConfiguration { provider: string; active: boolean; allowCard: boolean; allowUPI: boolean; allowNetBanking: boolean; allowCOD: boolean; outcome: number; version: string | null; sandbox: boolean; }
export interface PaymentHistory { attempts: { action: string; previous: number | null; current: number; createdAt: string; providerReference: string }[]; refunds: { id: string; amount: number; reason: string; providerReference: string; createdAt: string }[]; }
export interface PaymentReconciliation { consistent: boolean; sandbox: boolean; captured: number; refunded: number; refundCount: number; attemptCount: number; }
@Injectable({ providedIn: 'root' })
export class MerchantPaymentService {
  private readonly http = inject(HttpClient);
  private base(tenant: string) { return '/commerce/payment/tenants/' + encodeURIComponent(tenant) + '/payments'; }
  private options(reason: string) { return { headers: new HttpHeaders(reason.trim() ? { 'X-Support-Reason': reason.trim() } : {}) }; }
  list(tenant: string, page: number, status: number | null, reason: string) {
    return this.http.get<{ items: MerchantPayment[]; total: number; page: number; pageSize: number }>(this.base(tenant), { ...this.options(reason), params: status === null ? { page } : { page, status } }).pipe(timeout(15000));
  }
  configuration(tenant: string, reason: string) { return this.http.get<PaymentConfiguration>(this.base(tenant) + '/configuration', this.options(reason)).pipe(timeout(15000)); }
  saveConfiguration(tenant: string, value: PaymentConfiguration, reason: string) {
    const { active, allowCard, allowUPI, allowNetBanking, allowCOD, outcome, version } = value;
    return this.http.put<PaymentConfiguration>(this.base(tenant) + '/configuration', { active, allowCard, allowUPI, allowNetBanking, allowCOD, outcome, version }, this.options(reason)).pipe(timeout(15000));
  }
  history(tenant: string, order: string, reason: string) { return this.http.get<PaymentHistory>(this.base(tenant) + '/orders/' + encodeURIComponent(order) + '/history', this.options(reason)).pipe(timeout(15000)); }
  reconcile(tenant: string, order: string, reason: string) { return this.http.post<PaymentReconciliation>(this.base(tenant) + '/orders/' + encodeURIComponent(order) + '/reconcile', {}, this.options(reason)).pipe(timeout(15000)); }
  decide(tenant: string, payment: MerchantPayment, id: string, outcome: string, reason: string) {
    return this.http.post<{ accepted: boolean; applied: boolean }>(this.base(tenant) + '/orders/' + encodeURIComponent(payment.orderId) + '/sandbox-decision', { id, version: payment.version, outcome }, this.options(reason)).pipe(timeout(15000));
  }
}
