// src/app/features/public/category/category.component.ts
import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { Product, PagedResult, Category } from '../../../core/models/catalog.models';
import { CartService } from '../../../core/services/cart.service';

@Component({
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './category.component.html'
})
export class CategoryComponent {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  cart = inject(CartService);

  products = signal<Product[]>([]);
  total = signal(0);
  page = signal(1);
  pageSize = 24;
  error = signal<string | null>(null);

  category = signal<Category | null>(null);

  q = '';
  sort = 'newest';
  placeholder = '/assets/Images/categories/home-decor.jpg';

  slug = signal('');
  totalPages = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize)));

  constructor() {
    this.route.paramMap.subscribe(params => {
      const s = params.get('slug') || '';
      this.slug.set(s);
      this.q = this.route.snapshot.queryParamMap.get('q') || '';
      this.page.set(1);
      this.loadCategoryAndProducts();
    });

    this.route.queryParamMap.subscribe(params => {
      const nextQ = params.get('q') || '';
      if (nextQ !== this.q) {
        this.q = nextQ;
        this.page.set(1);
        this.loadCategoryAndProducts();
      }
    });
  }

  private loadCategoryAndProducts() {
    const slug = this.slug();
    if (!slug) {
      this.category.set(null);
      this.error.set(null);
      this.loadProducts();
      return;
    }

    this.api.get<Category>(`/categories/${slug}`).subscribe({
      next: cat => {
        this.category.set(cat);
        this.error.set(null);
        this.loadProducts(cat.id);
      },
      error: err => {
        console.error('Failed to load category', err);
        this.category.set(null);
        this.products.set([]);
        this.total.set(0);
        this.error.set(err?.status === 404 ? 'Category not found.' : 'Failed to load category.');
      }
    });
  }

  private loadProducts(categoryId?: number) {
    this.api.get<PagedResult<Product>>('/products', {
      q: this.q || undefined,
      page: this.page(),
      pageSize: this.pageSize,
      sort: this.sort || undefined,
      categoryId: categoryId ?? undefined
    }).subscribe({
      next: x => {
        this.products.set(x.items);
        this.total.set(x.totalItems);
      },
      error: err => {
        console.error('Failed to load products', err);
        this.products.set([]);
        this.total.set(0);
        this.error.set('Failed to load products.');
      }
    });
  }

  reload() {
    const cat = this.category();
    this.page.set(1);
    this.loadProducts(cat?.id);
  }

  next() {
    if (this.page() < this.totalPages()) {
      this.page.update(p => p + 1);
      const cat = this.category();
      this.loadProducts(cat?.id);
    }
  }

  prev() {
    if (this.page() > 1) {
      this.page.update(p => p - 1);
      const cat = this.category();
      this.loadProducts(cat?.id);
    }
  }
}
