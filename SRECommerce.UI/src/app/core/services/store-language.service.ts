import { Injectable, afterNextRender, signal } from '@angular/core';
import { CommerceProduct } from '../models/commerce.models';

import { hindi } from './store-language.hi';

@Injectable({ providedIn: 'root' })
export class StoreLanguageService {
  readonly language = signal<'en' | 'hi'>('en');
  constructor() {
    afterNextRender(() => {
      try { if (localStorage.getItem('srecommerce.store-language') === 'hi') this.setLanguage('hi'); } catch { /* Storage is optional. */ }
    });
  }
  setLanguage(value: string) {
    if (value !== 'en' && value !== 'hi') return;
    this.language.set(value);
    try { localStorage.setItem('srecommerce.store-language', value); } catch { /* Private browsing must still work. */ }
  }
  text(english: string) { return this.language() === 'hi' && Object.hasOwn(hindi, english) ? hindi[english] : english; }
  product<T extends Pick<CommerceProduct, 'name' | 'description' | 'seoTitle' | 'seoDescription' | 'translations'>>(value: T): T {
    const translation = value.translations?.[this.language()];
    return translation ? { ...value, name: translation.name || value.name, description: translation.description ?? value.description,
      seoTitle: translation.seoTitle || value.seoTitle, seoDescription: translation.seoDescription || value.seoDescription } : value;
  }
}
