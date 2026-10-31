// src/app/features/public/product-detail/product-detail.component.ts
import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { Product } from '../../../core/models/catalog.models';
import { CartService } from '../../../core/services/cart.service';

@Component({
  standalone: true,
  imports: [CommonModule],
  templateUrl: './product-detail.component.html'
})
export class ProductDetailComponent {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  cart = inject(CartService);

  p = signal<Product | undefined>(undefined);
  added = signal(false);
  placeholder = 'https://via.placeholder.com/600x400?text=Product';

  constructor() {
    const slug = this.route.snapshot.paramMap.get('slug')!;
    this.api.get<Product>(`/products/${slug}`)
      .subscribe(x => this.p.set(x));
  }

  add(prod: Product) {
    this.cart.add(prod);
    this.added.set(true);
    setTimeout(() => this.added.set(false), 1800);
  }
}
