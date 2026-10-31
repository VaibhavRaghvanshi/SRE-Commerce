import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { Category } from '../../../core/models/catalog.models';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin-categories.component.html'
})
export class AdminCategoriesComponent {
  private api = inject(ApiService);

  categories = signal<Category[]>([]);
  editingId: number | null = null;
  uploading = signal(false);
  selectedFile: File | null = null;
  search = '';

  form = {
    name: '',
    slug: '',
    parentId: null as number | null,
    imageUrl: ''
  };

  constructor() {
    this.load();
  }

  load() {
    this.api.get<Category[]>('/categories')
      .subscribe(res => this.categories.set(res));
  }

  filteredCategories(): Category[] {
    const q = this.search.trim().toLowerCase();
    if (!q) return this.categories();
    return this.categories().filter(c => {
      return (c.name || '').toLowerCase().includes(q)
        || (c.slug || '').toLowerCase().includes(q)
        || String(c.id || '').includes(q);
    });
  }

  edit(cat: Category) {
    this.editingId = cat.id;
    this.selectedFile = null;
    this.form = {
      name: cat.name,
      slug: cat.slug,
      parentId: cat.parentId ?? null,
      imageUrl: cat.imageUrl ?? ''
    };
  }

  cancel() {
    this.editingId = null;
    this.reset();
  }

  save() {
    if (!this.form.name.trim()) return;

    const persist = () => {
      const payload: any = { ...this.form };
      if (!payload.imageUrl || !payload.imageUrl.trim()) {
        delete payload.imageUrl;
      }

      if (this.editingId) {
        this.api.put(`/categories/${this.editingId}`, payload)
          .subscribe(() => {
            this.cancel();
            this.load();
          });
      } else {
        this.api.post('/categories', payload)
          .subscribe(() => {
            this.reset();
            this.load();
          });
      }
    };

    if (this.selectedFile) {
      this.uploading.set(true);
      this.api.uploadImage(this.selectedFile).subscribe({
        next: res => {
          this.form.imageUrl = res.url;
          this.selectedFile = null;
          this.uploading.set(false);
          persist();
        },
        error: () => {
          this.uploading.set(false);
        }
      });
      return;
    }

    persist();
  }

  remove(id: number) {
    if (!confirm('Delete category?')) return;

    this.api.delete(`/categories/${id}`)
      .subscribe(() => this.load());
  }

  reset() {
    this.form = {
      name: '',
      slug: '',
      parentId: null,
      imageUrl: ''
    };
    this.selectedFile = null;
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
