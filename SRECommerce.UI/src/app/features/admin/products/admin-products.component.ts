import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { Category, Product, PagedResult } from '../../../core/models/catalog.models';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin-products.component.html'
})
export class AdminProductsComponent {
  private api = inject(ApiService);

  categories = signal<Category[]>([]);
  products = signal<Product[]>([]);
  total = signal(0);

  page = signal(1);
  pageSize = 25;

  editingId: number | null = null;
  uploading = signal(false);
  selectedFile: File | null = null;
  search = '';

  form = {
    name: '',
    slug: '',
    sku: '',
    price: 0,
    discount: 0,
    stockQty: 0,
    categoryId: null as number | null,
    imageUrl: ''
  };

  constructor() {
    this.loadCategories();
    this.loadProducts();
  }

  loadCategories() {
    this.api.get<Category[]>('/categories')
      .subscribe(res => this.categories.set(res));
  }

  loadProducts() {
    this.api.get<PagedResult<Product>>('/products', {
      page: this.page(),
      pageSize: this.pageSize,
      q: this.search || undefined
    }).subscribe(x => {
      this.products.set(x.items);
      this.total.set(x.totalItems);
    });
  }

  searchProducts() {
    this.page.set(1);
    this.loadProducts();
  }

  filteredProducts(): Product[] {
    const q = this.search.trim().toLowerCase();
    if (!q) return this.products();
    return this.products().filter(p => {
      const name = (p.name || '').toLowerCase();
      const slug = (p.slug || '').toLowerCase();
      const sku = (p.sku || '').toLowerCase();
      const price = String(p.price ?? '');
      const id = String(p.id ?? '');
      return name.includes(q) || slug.includes(q) || sku.includes(q) || price.includes(q) || id.includes(q);
    });
  }

  edit(p: Product) {
    this.editingId = p.id;
    this.form = {
      name: p.name,
      slug: p.slug,
      sku: p.sku,
      price: p.price,
      discount: (p as any).discount ?? 0,
      stockQty: (p as any).stockQty ?? 0,
      categoryId: (p as any).categoryId ?? null,
      imageUrl: p.imageUrl ?? ''
    };
  }

  cancel() {
    this.editingId = null;
    this.reset();
  }

  save() {
    if (!this.form.name.trim() || !this.form.categoryId) return;

    const payload = {
      name: this.form.name,
      slug: this.form.slug || null,
      sku: this.form.sku,
      price: Number(this.form.price),
      discount: Number(this.form.discount) || 0,
      stockQty: Number(this.form.stockQty) || 0,
      categoryId: Number(this.form.categoryId),
      imageUrl: this.form.imageUrl || null
    };

    if (this.editingId) {
      this.api.put(`/products/${this.editingId}`, payload)
        .subscribe(() => {
          this.cancel();
          this.loadProducts();
        });
    } else {
      this.api.post('/products', payload)
        .subscribe(() => {
          this.reset();
          this.loadProducts();
        });
    }
  }

  remove(id: number) {
    if (!confirm('Delete product?')) return;
    this.api.delete(`/products/${id}`)
      .subscribe(() => this.loadProducts());
  }

  reset() {
    this.form = {
      name: '',
      slug: '',
      sku: '',
      price: 0,
      discount: 0,
      stockQty: 0,
      categoryId: null,
      imageUrl: ''
    };
  }

  next() {
    const pages = Math.max(1, Math.ceil(this.total() / this.pageSize));
    if (this.page() < pages) {
      this.page.update(p => p + 1);
      this.loadProducts();
    }
  }

  prev() {
    if (this.page() > 1) {
      this.page.update(p => p - 1);
      this.loadProducts();
    }
  }

  categoryName(id: number | null | undefined) {
    if (!id) return '-';
    return this.categories().find(x => x.id === id)?.name ?? '-';
  }

  getCategoryId(p: any): number | null {
    return p?.categoryId ?? null;
  }

  getStockQty(p: any): number | null {
    return p?.stockQty ?? null;
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
  }

  uploadImage() {
    if (!this.selectedFile) return;
    this.uploading.set(true);
    this.api.uploadImage(this.selectedFile).subscribe({
      next: res => {
        this.form.imageUrl = res.url;
        this.selectedFile = null;
        this.uploading.set(false);
      },
      error: () => {
        this.uploading.set(false);
      }
    });
  }
}
