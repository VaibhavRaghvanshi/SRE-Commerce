import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CommerceProduct } from '../models/commerce.models';
import { StoreLanguageService } from './store-language.service';

describe('Store language and published product translations', () => {
  let locale: StoreLanguageService;
  beforeEach(() => {
    localStorage.removeItem('srecommerce.store-language');
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection()] });
    locale = TestBed.inject(StoreLanguageService);
  });
  afterEach(() => localStorage.removeItem('srecommerce.store-language'));
  it('switches shopping controls to Hindi, persists preference and retains unknown copy as fallback', () => {
    locale.setLanguage('hi'); expect(locale.text('Add to cart')).toBe('कार्ट में जोड़ें');
    expect(locale.text('Your shopping cart')).toBe('आपका खरीदारी कार्ट');
    expect(localStorage.getItem('srecommerce.store-language')).toBe('hi');
    expect(locale.text('Merchant supplied text')).toBe('Merchant supplied text');
    expect(locale.text('__proto__')).toBe('__proto__');
    locale.setLanguage('en'); expect(locale.text('Add to cart')).toBe('Add to cart');
  });
  it('ignores unsupported language values', () => {
    locale.setLanguage('../../hi'); expect(locale.language()).toBe('en');
    expect(localStorage.getItem('srecommerce.store-language')).toBeNull();
  });
  it('uses published text without mutating the original product or replacing its identity', () => {
    const product = { id:'original',slug:'original',name:'Base name',description:'Base description',seoTitle:'Base SEO',seoDescription:'Base summary',
      translations:{hi:{name:'मसाला',description:'ताज़ा मसाला',seoTitle:'',seoDescription:'',id:'unexpected'}} } as unknown as CommerceProduct;
    locale.setLanguage('hi'); const translated=locale.product(product);
    expect(translated.name).toBe('मसाला'); expect(translated.seoTitle).toBe('Base SEO');
    expect(translated.id).toBe('original'); expect(translated.slug).toBe('original'); expect(product.name).toBe('Base name');
  });
  it('falls back to base content for older publications without translations', () => {
    const product={name:'Original product',description:'Original description',seoTitle:'',seoDescription:''};
    locale.setLanguage('hi'); expect(locale.product(product)).toBe(product);
  });
});
