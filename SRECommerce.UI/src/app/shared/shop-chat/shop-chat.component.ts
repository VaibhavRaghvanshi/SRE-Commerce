import { CommonModule } from '@angular/common';
import { Component, PLATFORM_ID, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { isPlatformBrowser } from '@angular/common';
import { ApiService } from '../../core/services/api.service';
import { Product, PagedResult } from '../../core/models/catalog.models';
import { AuthService } from '../../core/services/auth.service';

interface ChatMessage {
  from: 'user' | 'bot';
  text: string;
}

@Component({
  selector: 'app-shop-chat',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './shop-chat.component.html'
})
export class ShopChatComponent {
  private api = inject(ApiService);
  auth = inject(AuthService);
  private platformId = inject(PLATFORM_ID);

  open = signal(false);
  input = '';
  products = signal<Product[]>([]);
  messages = signal<ChatMessage[]>([
    { from: 'bot', text: 'Ask for product picks, budget options, stock help, dashboard work, orders, or sales summary.' }
  ]);

  constructor() {
    if (!isPlatformBrowser(this.platformId)) return;

    this.api.get<PagedResult<Product>>('/products', { pageSize: 40, sort: 'newest' })
      .subscribe({
        next: res => this.products.set(res.items || []),
        error: () => this.products.set([])
      });
  }

  send(text = this.input) {
    const q = text.trim();
    if (!q) return;

    this.input = '';
    this.messages.update(x => [...x, { from: 'user', text: q }]);

    const found = this.recommend(q);
    const staffAnswer = this.staffHelp(q);
    const answer = staffAnswer || (found.length
      ? `Best picks: ${found.map(p => `${p.name} (Rs. ${Math.round(p.price - (p.discount || 0))})`).join(', ')}.`
      : 'Try mobiles, laptops, fashion, beauty, home decor, kitchen, low price, premium, orders, stock, or dashboard.');

    this.messages.update(x => [...x, { from: 'bot', text: answer }]);
  }

  staffHelp(query: string): string | null {
    if (!this.auth.isStaff()) return null;
    const q = query.toLowerCase();

    if (q.includes('order')) {
      return 'Open Dashboard > Orders to review purchases and their current status.';
    }

    if (q.includes('stock') || q.includes('inventory') || q.includes('product')) {
      return 'Open Dashboard > Products to add products, upload pictures, adjust price, discount, and stock.';
    }

    if (q.includes('banner') || q.includes('home')) {
      return 'Open Dashboard > Home Sections or Banners to control what customers see on the storefront.';
    }

    if (q.includes('user') || q.includes('employee') || q.includes('access')) {
      return this.auth.isAdmin()
        ? 'Open Dashboard > Users to manage account access.'
        : 'Ask an administrator to manage account access.';
    }

    if (q.includes('sale') || q.includes('report') || q.includes('summary')) {
      return 'The dashboard summary shows sales, orders, stock value, top products, and recent orders.';
    }

    return null;
  }

  recommend(query: string): Product[] {
    const q = query.toLowerCase();
    let list = this.products();

    if (q.includes('cheap') || q.includes('budget') || q.includes('low')) {
      return [...list].sort((a, b) => (a.price - (a.discount || 0)) - (b.price - (b.discount || 0))).slice(0, 3);
    }

    if (q.includes('premium') || q.includes('best')) {
      return [...list].sort((a, b) => b.price - a.price).slice(0, 3);
    }

    const words = q.split(/\s+/).filter(Boolean);
    list = list.filter(p => words.some(w =>
      p.name.toLowerCase().includes(w) ||
      p.sku.toLowerCase().includes(w) ||
      String(p.price).includes(w)
    ));

    return list.slice(0, 3);
  }

  quick(text: string) {
    this.open.set(true);
    this.send(text);
  }
}
