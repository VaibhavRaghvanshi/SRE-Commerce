import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { Banner } from '../../../core/models/banner.models';
import { environment } from '../../../../environments/environment';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin-banners.component.html'
})
export class AdminBannersComponent {
  private api = inject(ApiService);

  banners = signal<Banner[]>([]);
  uploading = signal(false);
  selectedFile: File | null = null;
  editingId: number | null = null;
  placeholder = '/assets/Images/categories/home-decor.jpg';

  form = {
    title: '',
    imageUrl: '',
    linkType: 0,
    linkValue: '',
    isEnabled: true,
    sortOrder: 1
  };

  constructor() {
    this.load();
  }

  load() {
    this.api.get<Banner[]>('/admin/banners').subscribe(res => this.banners.set(res));
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
  }

  upload() {
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

  edit(b: Banner) {
    this.editingId = b.id;
    this.form = {
      title: b.title,
      imageUrl: b.imageUrl,
      linkType: b.linkType,
      linkValue: b.linkValue ?? '',
      isEnabled: b.isEnabled,
      sortOrder: b.sortOrder
    };
  }

  cancel() {
    this.editingId = null;
    this.reset();
  }

  save() {
    if (!this.form.title.trim() || !this.form.imageUrl.trim()) return;

    const payload = {
      title: this.form.title.trim(),
      imageUrl: this.form.imageUrl.trim(),
      linkType: Number(this.form.linkType),
      linkValue: this.form.linkValue?.trim() || null,
      isEnabled: !!this.form.isEnabled,
      sortOrder: Number(this.form.sortOrder) || 1
    };

    if (this.editingId) {
      this.api.put(`/admin/banners/${this.editingId}`, payload).subscribe(() => {
        this.cancel();
        this.load();
      });
    } else {
      this.api.post('/admin/banners', payload).subscribe(() => {
        this.reset();
        this.load();
      });
    }
  }

  remove(id: number) {
    if (!confirm('Delete banner?')) return;
    this.api.delete(`/admin/banners/${id}`).subscribe(() => this.load());
  }

  reset() {
    this.form = {
      title: '',
      imageUrl: '',
      linkType: 0,
      linkValue: '',
      isEnabled: true,
      sortOrder: 1
    };
  }

  bannerSrc(url: string) {
    if (!url) return this.placeholder;
    if (/^https?:\/\//i.test(url)) return url;
    const base = environment.apiBase.replace(/\/api\/?$/, '');
    return url.startsWith('/') ? `${base}${url}` : url;
  }
}
