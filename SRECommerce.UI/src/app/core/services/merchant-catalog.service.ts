import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { timeout } from 'rxjs';
import { CommerceLookup, CommercePage, CommerceProduct } from '../models/commerce.models';
export interface CatalogDefinition { id: string; code: string; name: string; dataType: number; options: string[]; required: boolean; unit: string | null; }
export interface MerchantLookup extends CommerceLookup { version: string; archived: boolean; }
export interface MerchantCollection extends MerchantLookup { productIds: string[]; }
export interface MerchantLookups { categories: MerchantLookup[]; brands: MerchantLookup[]; definitions: CatalogDefinition[]; collections: MerchantCollection[]; }
export interface ProductChoice { id: string; name: string; slug: string; status: number; publishedRevision: number; }
export interface OrganizationDraft { name: string; slug: string; version: string | null; archived: boolean; }
export interface CatalogEditor {
  name: string; slug: string; categoryId: string; brandId: string | null; kind: number; description: string; seoTitle: string; seoDescription: string;
  visible: boolean; b2BOnly: boolean; attributes: Record<string, string | number | boolean>; specifications: Record<string, string>;
  variants: { id: string | null; sku: string; options: Record<string, string>; barcode: string | null; active: boolean }[];
  media: { id: string | null; url: string; alt: string; order: number }[];
  bundleItems: { variantId: string; quantity: number }[];
  relationships: { productId: string; kind: string }[]; version: string | null;
}
@Injectable({ providedIn: 'root' })
export class MerchantCatalogService {
  private readonly http = inject(HttpClient);
  private options(reason: string) { return { headers: reason.trim() ? new HttpHeaders({ 'X-Support-Reason': reason.trim() }) : new HttpHeaders() }; }
  private base(tenant: string) { return `/commerce/catalog/tenants/${encodeURIComponent(tenant)}/catalog`; }
  uploadImage(tenant: string, file: File, reason: string) { const body = new FormData(); body.append('file', file); return this.http.post<{ id: string; url: string; width: number; height: number }>(this.base(tenant) + '/media-assets', body, this.options(reason)).pipe(timeout(20000)); }
  productChoices(tenant: string, ids: string[], reason: string) { return this.http.post<ProductChoice[]>(this.base(tenant) + '/product-choices', { ids }, this.options(reason)).pipe(timeout(15000)); }
  saveCategory(tenant: string, id: string, input: OrganizationDraft & { parentId: string | null }, reason: string) { return id ? this.http.put<MerchantLookup>(this.base(tenant) + '/categories/' + id, input, this.options(reason)) : this.http.post<MerchantLookup>(this.base(tenant) + '/categories', input, this.options(reason)); }
  saveBrand(tenant: string, id: string, input: OrganizationDraft, reason: string) { return id ? this.http.put<MerchantLookup>(this.base(tenant) + '/brands/' + id, input, this.options(reason)) : this.http.post<MerchantLookup>(this.base(tenant) + '/brands', input, this.options(reason)); }
  saveCollection(tenant: string, id: string, input: OrganizationDraft & { productIds: string[] }, reason: string) { return id ? this.http.put<MerchantCollection>(this.base(tenant) + '/collections/' + id, input, this.options(reason)) : this.http.post<MerchantCollection>(this.base(tenant) + '/collections', input, this.options(reason)); }
  lookups(tenant: string, reason: string) { return this.http.get<MerchantLookups>(this.base(tenant) + '/lookups', this.options(reason)).pipe(timeout(15000)); }
  products(tenant: string, page: number, reason: string, search='') { return this.http.get<CommercePage<CommerceProduct>>(this.base(tenant) + '/products', { ...this.options(reason), params: { page, pageSize: 24, search } }).pipe(timeout(15000)); }
  attributes(tenant: string, category: string, reason: string) { return this.http.get<CatalogDefinition[]>(this.base(tenant) + `/categories/${category}/attributes`, this.options(reason)).pipe(timeout(15000)); }
  save(tenant: string, id: string | null, editor: CatalogEditor, reason: string) { return id ? this.http.put<CommerceProduct>(this.base(tenant) + '/products/' + id, editor, this.options(reason)) : this.http.post<CommerceProduct>(this.base(tenant) + '/products', editor, this.options(reason)); }
  publish(tenant: string, product: CommerceProduct, reason: string) { return this.http.post<CommerceProduct>(this.base(tenant) + `/products/${product.id}/publish`, { version: product.version }, this.options(reason)); }
  archive(tenant: string, product: CommerceProduct, reason: string) { return this.http.post(this.base(tenant) + `/products/${product.id}/archive`, { version: product.version }, this.options(reason)); }
  category(tenant: string, name: string, slug: string, reason: string) { return this.http.post(this.base(tenant) + '/categories', { name, slug, parentId: null }, this.options(reason)); }
  definition(tenant: string, value: object, reason: string) { return this.http.post(this.base(tenant) + '/attributes', value, this.options(reason)); }
  bind(tenant: string, category: string, version: string, bindings: { attributeId: string; required: boolean }[], reason: string) { return this.http.put(this.base(tenant) + `/categories/${category}/attributes`, bindings, { ...this.options(reason), params: { version } }); }
}
