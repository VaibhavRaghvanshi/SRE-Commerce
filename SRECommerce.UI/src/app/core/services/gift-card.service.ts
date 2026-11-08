import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, timeout } from 'rxjs';

export interface GiftCard {
  id: string;
  currency: string;
  value: number;
  balance: number;
  issuedAt: string;
  claimedAt: string | null;
  voidedAt: string | null;
  reference: string | null;
}

export interface GiftCardIssue { id: string; code: string; value: number; reference: string; }

@Injectable({ providedIn: 'root' })
export class GiftCardService {
  private readonly http = inject(HttpClient);
  private base(tenant: string) { return '/commerce/payment/tenants/' + encodeURIComponent(tenant) + '/payments/gift-cards'; }
  list(tenant: string, page: number, merchant = false) {
    return firstValueFrom(this.http.get<GiftCard[]>(this.base(tenant) + (merchant ? '' : '/mine'), { params: { page } }).pipe(timeout(15000)));
  }
  issue(tenant: string, request: GiftCardIssue) {
    return firstValueFrom(this.http.post<GiftCard>(this.base(tenant), request).pipe(timeout(15000)));
  }
  claim(tenant: string, code: string) {
    return firstValueFrom(this.http.post<GiftCard>(this.base(tenant) + '/claim', { code }).pipe(timeout(15000)));
  }
  void(tenant: string, id: string) {
    return firstValueFrom(this.http.post<GiftCard>(this.base(tenant) + '/' + encodeURIComponent(id) + '/void', {}).pipe(timeout(15000)));
  }
}
