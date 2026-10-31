import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

import { HomeService } from '../../../core/services/home.service';
import { HomeBlock, HomeResponse, HomeBanner } from '../../../core/models/home.models';
import { Category } from '../../../core/models/catalog.models';
import { Product } from '../../../core/models/catalog.models';
import { BannerSliderComponent } from '../../../shared/banner-slider/banner-slider.component';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterLink, BannerSliderComponent],
  templateUrl: './home.component.html'
})
export class HomeComponent {
  private homeSvc = inject(HomeService);

  loading = signal(true);
  error = signal<string | null>(null);

  blocks = signal<HomeBlock[]>([]);

  // handy
  orderedBlocks = computed(() => [...this.blocks()].sort((a, b) => a.sortOrder - b.sortOrder));
  categoryCount = computed(() => this.orderedBlocks()
    .filter(b => b.type === 1)
    .reduce((sum, b) => sum + this.asCategories(b).length, 0));
  productCount = computed(() => this.orderedBlocks()
    .filter(b => b.type === 3)
    .reduce((sum, b) => sum + this.asProducts(b).length, 0));
  departmentCategories = computed(() => {
    const categoryBlock = this.orderedBlocks().find(b => b.type === 1);
    return categoryBlock ? this.asCategories(categoryBlock).slice(0, 9) : [];
  });
  heroProduct = computed(() => this.orderedBlocks()
    .flatMap(b => b.type === 3 ? this.asProducts(b) : [])
    .find(Boolean) || null);

  placeholder = '/assets/Images/categories/home-decor.jpg';

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.error.set(null);

    this.homeSvc.getHome().subscribe({
      next: (res: HomeResponse) => {
        this.blocks.set(res?.sections ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Storefront data is unavailable right now. Please try again.');
        this.blocks.set([]);
        this.loading.set(false);
      }
    });
  }

  // casting helpers
  asCategories(b: HomeBlock): Category[] {
    return (b.items ?? []) as Category[];
  }

  asProducts(b: HomeBlock): Product[] {
    return (b.items ?? []) as Product[];
  }

  asBanners(b: HomeBlock): HomeBanner[] {
    return (b.items ?? []) as HomeBanner[];
  }

  gridStyle(cardsPerRow: number) {
    const n = Math.max(1, Math.min(6, cardsPerRow || 4));
    return {
      'grid-template-columns': `repeat(${n}, minmax(0, 1fr))`
    };
  }

  sectionConfig(b: HomeBlock): any {
    if (!b.styleJson) return {};
    try {
      return JSON.parse(b.styleJson);
    } catch {
      return {};
    }
  }

  sectionClass(b: HomeBlock): string {
    const cfg = this.sectionConfig(b);
    return [
      'home-sec',
      cfg.variant ? `section-${cfg.variant}` : '',
      cfg.tone ? `tone-${cfg.tone}` : ''
    ].filter(Boolean).join(' ');
  }

  sectionStyle(b: HomeBlock) {
    const cfg = this.sectionConfig(b);
    return {
      '--section-accent': cfg.accent || '',
      '--section-bg': cfg.bg || ''
    };
  }

  salePercent(p: Product): number {
    if (!p.discount || !p.price) return 0;
    return Math.round((p.discount / p.price) * 100);
  }

  trackById = (_: number, x: any) => x?.id ?? _;
}
