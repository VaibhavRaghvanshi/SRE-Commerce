import { computed, provideZonelessChangeDetection, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { RelatedProductsComponent } from './related-products.component';
import { CommerceProduct } from '../../../core/models/commerce.models';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
import { commerceAuthInterceptor } from '../../../core/interceptors/commerce-auth.interceptor';

const tenant = '11111111-1111-1111-1111-111111111111';
function product(id: string, relationships: { productId: string; kind: string }[] = []): CommerceProduct {
  return { id, name: 'Published ' + id, slug: id, media: [], relationships } as unknown as CommerceProduct;
}
describe('Related product audience and request isolation', () => {
  const member = signal(true); let http: HttpTestingController;
  beforeEach(() => {
    member.set(true);
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection(), provideRouter([]), provideHttpClient(withInterceptors([commerceAuthInterceptor])), provideHttpClientTesting(),
      { provide: CommerceSessionService, useValue: { get token() { return member() ? 'ephemeral-test-bearer' : null; }, access: computed(() => member() ? { selectedTenantId: tenant } : null) } }] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function view(value: CommerceProduct) {
    const fixture = TestBed.createComponent(RelatedProductsComponent);
    fixture.componentRef.setInput('tenant', tenant); fixture.componentRef.setInput('store', 'legacy-demo'); fixture.componentRef.setInput('product', value); fixture.detectChanges(); return fixture;
  }
  it('omits missing publications and clears private suggestions before a guest lookup on logout', () => {
    const fixture = view(product('root', [{ productId: 'eligible', kind: 'related' }, { productId: 'ineligible', kind: 'upsell' }, { productId: 'root', kind: 'cross-sell' }]));
    const request = http.expectOne(r => r.url.endsWith('/catalog/buyer/published-products'));
    expect(request.request.headers.get('Authorization')).toBe('Bearer ephemeral-test-bearer'); expect(request.request.params.getAll('ids')).toEqual(['eligible', 'ineligible']);
    request.flush([product('eligible')]); fixture.detectChanges();
    expect(fixture.componentInstance.items().map(row => row.product.id)).toEqual(['eligible']);
    member.set(false); fixture.detectChanges(); expect(fixture.componentInstance.items()).toEqual([]);
    const publicRequest = http.expectOne(r => r.url.includes('/catalog/public/') && r.url.endsWith('/published-products'));
    expect(publicRequest.request.headers.has('Authorization')).toBeFalse(); publicRequest.flush([]); fixture.destroy();
  });
  it('cancels the previous product lookup instead of showing its late results on the next detail page', () => {
    const fixture = view(product('first', [{ productId: 'old', kind: 'related' }]));
    const old = http.expectOne(r => r.url.endsWith('/catalog/buyer/published-products'));
    fixture.componentRef.setInput('product', product('next', [{ productId: 'new', kind: 'cross-sell' }])); fixture.detectChanges();
    expect(old.cancelled).toBeTrue();
    const current = http.expectOne(r => r.url.endsWith('/catalog/buyer/published-products')); expect(current.request.params.getAll('ids')).toEqual(['new']);
    current.flush([product('new')]); expect(fixture.componentInstance.items()[0].product.id).toBe('new'); fixture.destroy();
  });
});
