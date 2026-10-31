import { Component, Input, signal, effect, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { HomeBanner } from '../../core/models/home.models';
import { environment } from '../../../environments/environment';

@Component({
  standalone: true,
  selector: 'app-banner-slider',
  imports: [CommonModule, RouterLink],
  templateUrl: './banner-slider.component.html'
})
export class BannerSliderComponent implements OnDestroy {
  @Input({ required: true }) banners: HomeBanner[] = [];
  @Input() autoSlide = true;
  @Input() intervalMs = 3500;

  idx = signal(0);
  private timer: any = null;

  constructor() {
    effect(() => {
      if (!this.banners || this.banners.length === 0) {
        this.idx.set(0);
        this.stop();
        return;
      }
      if (this.idx() >= this.banners.length) this.idx.set(0);

      if (this.autoSlide) this.start();
      else this.stop();
    });
  }

  ngOnDestroy(): void {
    this.stop();
  }

  start() {
    this.stop();
    if (!this.banners || this.banners.length <= 1) return;

    this.timer = setInterval(() => {
      this.next();
    }, this.intervalMs);
  }

  stop() {
    if (this.timer) clearInterval(this.timer);
    this.timer = null;
  }

  prev() {
    if (!this.banners || this.banners.length === 0) return;
    const n = this.banners.length;
    this.idx.set((this.idx() - 1 + n) % n);
  }

  next() {
    if (!this.banners || this.banners.length === 0) return;
    const n = this.banners.length;
    this.idx.set((this.idx() + 1) % n);
  }

  go(i: number) {
    if (!this.banners || this.banners.length === 0) return;
    if (i < 0 || i >= this.banners.length) return;
    this.idx.set(i);
  }

  trackByIndex = (i: number) => i;

  isExternal(link: string) {
    return /^https?:\/\//i.test(link);
  }

  imageSrc(url: string) {
    if (!url) return '/assets/Images/categories/home-decor.jpg';
    if (/^https?:\/\//i.test(url)) return url;
    const base = environment.apiBase.replace(/\/api\/?$/, '');
    return url.startsWith('/') ? `${base}${url}` : url;
  }
}
