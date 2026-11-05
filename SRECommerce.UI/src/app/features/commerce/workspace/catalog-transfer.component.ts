import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, Input, Output, EventEmitter, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CatalogEditor, MerchantCatalogService, MerchantLookups } from '../../../core/services/merchant-catalog.service';
import { CommerceProduct } from '../../../core/models/commerce.models';
import { catalogCsvTemplate, catalogDraftsFromCsv } from './catalog-csv';

@Component({ selector: 'app-catalog-transfer', standalone: true, imports: [CommonModule], templateUrl: './catalog-transfer.component.html', styleUrl: './catalog-transfer.component.scss' })
export class CatalogTransferComponent {
  @Input({ required: true }) tenant = ''; @Input({ required: true }) lookups: MerchantLookups = { categories: [], brands: [], definitions: [], collections: [] }; @Input() supportReason = '';
  @Output() changed = new EventEmitter<void>(); private readonly api = inject(MerchantCatalogService);
  readonly busy = signal(false); readonly exporting = signal(false); readonly products = signal<CatalogEditor[]>([]); readonly error = signal(''); readonly message = signal(''); readonly uncertain = signal(false);
  private attempt: { key: string; source: string; products: CatalogEditor[] } | null = null;
  private download(text: string, type: string, name: string) {
    const url = URL.createObjectURL(new Blob([text], { type })), link = document.createElement('a'); link.href = url; link.download = name; link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
  template() {
    const category = this.lookups.categories.find(item => !item.archived); if (!category) { this.error.set('Create an active category before preparing an import.'); return; }
    this.download(catalogCsvTemplate(category.slug), 'text/csv;charset=utf-8', 'srecommerce-new-products-template.csv');
  }
  async choose(event: Event) {
    if (this.busy() || this.attempt) return; const input = event.target as HTMLInputElement, file = input.files?.[0]; if (!file) return;
    this.error.set(''); this.message.set(''); this.products.set([]);
    try {
      if (!/\.csv$/i.test(file.name) || file.size > 750000) throw new Error('Choose a UTF-8 CSV file up to 750 KB.');
      const products = catalogDraftsFromCsv(await file.text(), this.lookups);
      if (new TextEncoder().encode(JSON.stringify(products)).length > 950000) throw new Error('This import is too large. Split the file into smaller batches.');
      this.products.set(products);
    } catch (error) { this.error.set(error instanceof Error ? error.message : 'The CSV could not be read.'); }
    finally { input.value = ''; }
  }
  clear() { if (!this.attempt && !this.busy()) { this.products.set([]); this.error.set(''); } }
  async apply() {
    if (this.busy() || !this.products().length) return; this.busy.set(true); this.error.set(''); this.message.set('');
    this.attempt ??= { key: crypto.randomUUID(), source: 'merchant-csv', products: structuredClone(this.products()) };
    const attempt = this.attempt;
    try {
      const ids = await firstValueFrom(this.api.importDrafts(this.tenant, attempt.key, attempt.source, attempt.products, this.supportReason));
      if (!Array.isArray(ids) || ids.length !== attempt.products.length || ids.some(id => !/^[a-f0-9-]{36}$/i.test(id))) throw new Error('Unconfirmed import result');
      this.attempt = null; this.uncertain.set(false); this.products.set([]); this.message.set(ids.length + ' new drafts created. Review each product before publishing.'); this.changed.emit();
    } catch (error) {
      const rejected = error instanceof HttpErrorResponse && error.status >= 400 && error.status < 500 && ![408, 429].includes(error.status);
      if (rejected) { this.attempt = null; this.uncertain.set(false); this.error.set(error.status === 409 ? 'The batch conflicts with existing slugs/SKUs or catalog settings. Nothing was overwritten. Correct the file before retrying.' : 'The import was rejected. Check the category fields, SKU and product values.'); }
      else { this.uncertain.set(true); this.error.set('The result could not be confirmed. Retry this same batch to check its saved result; another batch is locked until this is resolved.'); }
    } finally { this.busy.set(false); }
  }
  async export() {
    if (this.exporting()) return; this.exporting.set(true); this.error.set(''); this.message.set('');
    try {
      const products: CommerceProduct[] = []; const seen = new Set<string>(); let expected: number | null = null;
      for (let page = 1; page <= 50; page++) {
        const result = await firstValueFrom(this.api.exportPage(this.tenant, page, this.supportReason));
        if (result.total > 5000) throw new Error('Export supports up to 5,000 products per file.');
        expected ??= result.total; if (result.total !== expected) throw new Error('The catalog changed while exporting. Retry when edits are finished.');
        for (const product of result.items) { if (seen.has(product.id)) throw new Error('The catalog changed while exporting. Retry when edits are finished.'); seen.add(product.id); products.push(product); }
        if (products.length >= expected) break;
        if (!result.items.length || page === 50) throw new Error('The catalog export was incomplete. Try again.');
      }
      const json = JSON.stringify({ schemaVersion: 1, tenantId: this.tenant, exportedAt: new Date().toISOString(), products }, null, 2);
      if (new TextEncoder().encode(json).length > 10000000) throw new Error('This catalog snapshot exceeds the 10 MB file limit.');
      this.download(json, 'application/json', 'srecommerce-catalog-' + new Date().toISOString().slice(0, 10) + '.json'); this.message.set(products.length + ' catalog records exported. Pricing and stock are managed separately.');
    } catch (error) { this.error.set(error instanceof Error && !(error instanceof HttpErrorResponse) ? error.message : 'The catalog export could not be completed. Try again.'); }
    finally { this.exporting.set(false); }
  }
}
