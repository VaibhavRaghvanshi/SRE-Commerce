import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { MerchantCatalogService, MerchantLookups, OrganizationDraft, ProductChoice } from '../../../core/services/merchant-catalog.service';

type Draft = OrganizationDraft & { id: string };
type CategoryDraft = Draft & { parentId: string | null };
type CollectionDraft = Draft & { productIds: string[] };
const blank = (): Draft => ({ id: '', name: '', slug: '', version: null, archived: false });

@Component({ selector: 'app-catalog-organization', standalone: true, imports: [CommonModule, FormsModule],
  templateUrl: './catalog-organization.component.html', styleUrl: './catalog-organization.component.scss' })
export class CatalogOrganizationComponent {
  @Input({ required: true }) tenant = '';
  @Input({ required: true }) lookups: MerchantLookups = { categories: [], brands: [], definitions: [], collections: [] };
  @Input() supportReason = '';
  @Output() changed = new EventEmitter<void>();
  private readonly api = inject(MerchantCatalogService);
  readonly busy = signal(false); readonly error = signal(''); readonly message = signal('');
  readonly candidates = signal<ProductChoice[]>([]); readonly labels = signal<Record<string, ProductChoice>>({});
  readonly candidateTotal = signal(0);
  category: CategoryDraft = { ...blank(), parentId: null }; brand: Draft = blank();
  collection: CollectionDraft = { ...blank(), productIds: [] };
  query = ''; page = 1;

  private async run(action: () => Promise<void>) {
    if (this.busy()) return; this.busy.set(true); this.error.set(''); this.message.set('');
    try { await action(); }
    catch (e) {
      const code = e instanceof HttpErrorResponse ? e.error?.title : '';
      this.error.set(code === 'CategoryCycle' ? 'Choose a parent outside this category’s descendants.' :
        code === 'ArchiveChildCategoriesFirst' ? 'Archive the child categories first.' :
        e instanceof HttpErrorResponse && e.status === 409 ? 'This name, URL or version conflicts with existing data. Refresh and reload the selected record.' :
          'Catalog organization could not be confirmed. Check your access and try again.');
    } finally { this.busy.set(false); }
  }
  selectCategory(id: string) {
    const row = this.lookups.categories.find(x => x.id === id);
    this.category = row ? { ...row, parentId: row.parentId || null } : { ...blank(), parentId: null };
  }
  selectBrand(id: string) { const row = this.lookups.brands.find(x => x.id === id); this.brand = row ? { ...row } : blank(); }
  selectCollection(id: string) {
    const row = this.lookups.collections?.find(x => x.id === id);
    this.collection = row ? { ...row, productIds: [...row.productIds] } : { ...blank(), productIds: [] };
    this.candidates.set([]); this.labels.set({}); this.page = 1; this.query = ''; this.candidateTotal.set(0);
    if (row?.productIds.length) return this.run(async () => {
      const selected = this.collection;
      const products = await firstValueFrom(this.api.productChoices(this.tenant, row.productIds, this.supportReason));
      if (this.collection === selected) this.labels.set(Object.fromEntries(products.map(p => [p.id, p])));
    });
    return Promise.resolve();
  }
  parentCategories() { return this.lookups.categories.filter(row => !row.archived && row.id !== this.category.id); }
  saveCategory() { return this.run(async () => {
    const { id, name, slug, parentId, archived, version } = this.category;
    const saved = await firstValueFrom(this.api.saveCategory(this.tenant, id, { name, slug, parentId, archived, version }, this.supportReason));
    this.category = { ...saved, parentId: saved.parentId || null }; this.message.set('Category saved.'); this.changed.emit();
  }); }
  saveBrand() { return this.run(async () => {
    const { id, name, slug, archived, version } = this.brand;
    this.brand = await firstValueFrom(this.api.saveBrand(this.tenant, id, { name, slug, archived, version }, this.supportReason));
    this.message.set('Brand saved.'); this.changed.emit();
  }); }
  saveCollection() { return this.run(async () => {
    const { id, name, slug, archived, version, productIds } = this.collection;
    this.collection = await firstValueFrom(this.api.saveCollection(this.tenant, id, { name, slug, archived, version, productIds }, this.supportReason));
    this.message.set('Collection saved. Only eligible published products appear in the storefront.'); this.changed.emit();
  }); }
  search(page = 1) { return this.run(async () => {
    const result = await firstValueFrom(this.api.products(this.tenant, page, this.supportReason, this.query.trim()));
    this.page = result.page; this.candidateTotal.set(result.total);
    const choices = result.items.map(p => ({ id: p.id, name: p.name, slug: p.slug, status: p.status, publishedRevision: p.publishedRevision }));
    this.candidates.set(choices); this.labels.update(rows => ({ ...rows, ...Object.fromEntries(choices.map(p => [p.id, p])) }));
  }); }
  add(product: ProductChoice) {
    if (this.busy() || product.status === 2 || this.collection.productIds.includes(product.id) || this.collection.productIds.length >= 200) return;
    this.collection.productIds = [...this.collection.productIds, product.id]; this.labels.update(rows => ({ ...rows, [product.id]: product }));
  }
  remove(id: string) { if (!this.busy()) this.collection.productIds = this.collection.productIds.filter(p => p !== id); }
  move(index: number, direction: number) {
    const target = index + direction, ids = [...this.collection.productIds];
    if (this.busy() || target < 0 || target >= ids.length) return;
    [ids[index], ids[target]] = [ids[target], ids[index]]; this.collection.productIds = ids;
  }
}
