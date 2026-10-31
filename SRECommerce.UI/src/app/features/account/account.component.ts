import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Order } from '../../core/models/order.models';
import { OrderService } from '../../core/services/order.service';
import { AuthService } from '../../core/services/auth.service';

@Component({
  standalone: true,
  selector: 'app-account',
  imports: [CommonModule],
  templateUrl: './account.component.html'
})
export class AccountComponent {
  private orderService = inject(OrderService);
  auth = inject(AuthService);

  orders = signal<Order[]>([]);
  loading = signal(true);

  constructor() {
    this.orderService.mine().subscribe({
      next: orders => {
        this.orders.set(orders);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
