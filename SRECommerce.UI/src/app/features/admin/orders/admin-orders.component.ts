import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Order } from '../../../core/models/order.models';
import { OrderService } from '../../../core/services/order.service';

@Component({
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="container">
      <div class="d-flex items-center justify-between mb-3">
        <h2 class="m-0">Admin Orders</h2>
        <span class="text-muted">{{ orders().length }} orders</span>
      </div>

      <div *ngIf="loading()" class="alert">Loading...</div>

      <table class="table" *ngIf="!loading()">
        <thead>
          <tr>
            <th>Order</th>
            <th>Customer</th>
            <th>Items</th>
            <th>Total</th>
            <th>Status</th>
            <th>Date</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let order of orders()">
            <td>{{ order.orderNumber }}</td>
            <td>
              <strong>{{ order.customerName }}</strong><br />
              <small class="text-muted">{{ order.customerEmail }}</small>
            </td>
            <td>{{ itemCount(order) }}</td>
            <td>Rs. {{ order.total | number:'1.0-0' }}</td>
            <td><span class="badge">{{ order.status }}</span></td>
            <td>{{ order.createdAt | date:'medium' }}</td>
          </tr>
          <tr *ngIf="orders().length === 0">
            <td colspan="6">No orders yet.</td>
          </tr>
        </tbody>
      </table>
    </div>
  `
})
export class AdminOrdersComponent {
  private orderService = inject(OrderService);
  orders = signal<Order[]>([]);
  loading = signal(true);

  constructor() {
    this.orderService.adminList().subscribe({
      next: orders => {
        this.orders.set(orders);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  itemCount(order: Order): number {
    return order.items.reduce((sum, item) => sum + item.quantity, 0);
  }
}
