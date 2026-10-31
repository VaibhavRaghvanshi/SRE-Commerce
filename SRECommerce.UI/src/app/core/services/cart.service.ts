import { computed, Injectable, signal } from '@angular/core';
import { Product } from '../models/catalog.models';
import { CartItem } from '../models/order.models';

@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly key = 'sre_cart';
  private readonly itemsSignal = signal<CartItem[]>(this.read());

  readonly items = this.itemsSignal.asReadonly();
  readonly count = computed(() => this.itemsSignal().reduce((sum, item) => sum + item.quantity, 0));
  readonly subtotal = computed(() =>
    this.itemsSignal().reduce((sum, item) => sum + this.unitPrice(item.product) * item.quantity, 0)
  );

  add(product: Product, quantity = 1) {
    const next = [...this.itemsSignal()];
    const existing = next.find(x => x.product.id === product.id);
    const safeQty = Math.max(1, quantity);

    if (existing) {
      existing.quantity = Math.min(product.stockQty || 99, existing.quantity + safeQty);
    } else {
      next.push({ product, quantity: Math.min(product.stockQty || 99, safeQty) });
    }

    this.set(next);
  }

  update(productId: number, quantity: number) {
    const next = this.itemsSignal()
      .map(item => item.product.id === productId
        ? { ...item, quantity: Math.max(1, Math.min(item.product.stockQty || 99, quantity)) }
        : item)
      .filter(item => item.quantity > 0);
    this.set(next);
  }

  remove(productId: number) {
    this.set(this.itemsSignal().filter(item => item.product.id !== productId));
  }

  clear() {
    this.set([]);
  }

  unitPrice(product: Product): number {
    return Math.max(0, product.price - (product.discount || 0));
  }

  private set(items: CartItem[]) {
    this.itemsSignal.set(items);
    if (typeof window !== 'undefined' && typeof localStorage !== 'undefined') {
      localStorage.setItem(this.key, JSON.stringify(items));
    }
  }

  private read(): CartItem[] {
    if (typeof window === 'undefined' || typeof localStorage === 'undefined') return [];
    try {
      const raw = localStorage.getItem(this.key);
      return raw ? JSON.parse(raw) as CartItem[] : [];
    } catch {
      return [];
    }
  }
}
