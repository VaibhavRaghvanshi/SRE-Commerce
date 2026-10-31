import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { CartService } from '../../../core/services/cart.service';
import { OrderService } from '../../../core/services/order.service';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './cart.component.html'
})
export class CartComponent {
  cart = inject(CartService);
  auth = inject(AuthService);
  private orders = inject(OrderService);

  submitting = signal(false);
  error = signal<string | null>(null);
  success = signal<string | null>(null);
  shippingFee = computed(() => this.cart.count() > 0 ? 99 : 0);
  grandTotal = computed(() => this.cart.subtotal() + this.shippingFee());

  form = {
    customerName: this.auth.userName || '',
    customerEmail: '',
    phone: '',
    shippingAddress: ''
  };

  checkout() {
    this.error.set(null);
    this.success.set(null);

    if (!this.auth.isLoggedIn()) {
      this.error.set('Please login before checkout.');
      return;
    }

    if (!this.form.customerName.trim() || !this.form.customerEmail.trim() ||
        !this.form.phone.trim() || !this.form.shippingAddress.trim()) {
      this.error.set('Fill all checkout details.');
      return;
    }

    const items = this.cart.items().map(item => ({
      productId: item.product.id,
      quantity: item.quantity
    }));

    this.submitting.set(true);
    this.orders.create({ ...this.form, items }).subscribe({
      next: order => {
        this.cart.clear();
        this.success.set(`Order ${order.orderNumber} placed successfully.`);
        this.submitting.set(false);
      },
      error: err => {
        this.error.set(err?.error?.message || 'Checkout failed.');
        this.submitting.set(false);
      }
    });
  }
}
