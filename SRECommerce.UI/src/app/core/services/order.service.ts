import { Injectable, inject } from '@angular/core';
import { ApiService } from './api.service';
import { AdminSummary, CreateOrderRequest, Order } from '../models/order.models';

@Injectable({ providedIn: 'root' })
export class OrderService {
  private api = inject(ApiService);

  create(payload: CreateOrderRequest) {
    return this.api.post<Order>('/orders', payload);
  }

  mine() {
    return this.api.get<Order[]>('/orders/mine');
  }

  adminList() {
    return this.api.get<Order[]>('/orders/admin');
  }

  adminSummary() {
    return this.api.get<AdminSummary>('/admin/reports/summary');
  }
}
