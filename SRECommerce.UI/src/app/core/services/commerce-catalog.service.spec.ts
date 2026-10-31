import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CommerceCatalogService } from './commerce-catalog.service';
describe('Published Catalog API requests',()=>{
  beforeEach(()=>TestBed.configureTestingModule({providers:[provideZonelessChangeDetection(),provideHttpClient(),provideHttpClientTesting()]}));
  afterEach(()=>TestBed.inject(HttpTestingController).verify());
  it('sends pagination and typed filters to the selected tenant',()=>{
    TestBed.inject(CommerceCatalogService).products('tenant-a',2,'whole spices','category-a','','bundles').subscribe();
    const request=TestBed.inject(HttpTestingController).expectOne(r=>r.url==='/commerce/catalog/public/tenants/tenant-a/catalog/products');
    expect(request.request.params.get('page')).toBe('2');expect(request.request.params.get('search')).toBe('whole spices');expect(request.request.params.get('categoryId')).toBe('category-a');expect(request.request.params.has('brandId')).toBeFalse();request.flush({items:[],total:0,page:2,pageSize:24});
  });
  it('encodes a store slug instead of accepting it as a URL',()=>{TestBed.inject(CommerceCatalogService).tenant('other/store').subscribe();const request=TestBed.inject(HttpTestingController).expectOne('/commerce/tenant/public/tenants/other%2Fstore');expect(request.request.url).toContain('other%2Fstore');request.flush({});});
});
