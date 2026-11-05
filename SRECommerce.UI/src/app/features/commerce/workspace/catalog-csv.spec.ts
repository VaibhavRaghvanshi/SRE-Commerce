import { catalogDraftsFromCsv, readCatalogCsv } from './catalog-csv';
import { MerchantLookups } from '../../../core/services/merchant-catalog.service';

const lookups: MerchantLookups = { categories: [{ id: 'own-category', name: 'Spices', slug: 'spices', archived: false, version: 'v1' }], brands: [], definitions: [], collections: [] };
describe('Catalog CSV boundaries', () => {
  it('handles quoted commas, escaped quotes and multiline values without inventing extra products', () => {
    const csv = 'name,slug,category,sku,description\r\n"Pepper, whole",pepper,spices,pepper-1,"A ""fresh"" batch\r\nwith details"\r\n';
    const [product] = catalogDraftsFromCsv(csv, lookups);
    expect(product.name).toBe('Pepper, whole'); expect(product.description).toBe('A "fresh" batch\r\nwith details'); expect(product.variants[0].sku).toBe('PEPPER-1');
    expect(product.categoryId).toBe('own-category'); expect(product.version).toBeNull(); expect(product.variants[0].id).toBeNull();
  });
  it('rejects foreign categories, privileged columns, malformed quotes and duplicate SKU identities', () => {
    expect(() => catalogDraftsFromCsv('name,slug,category,sku\nItem,item,foreign,one', lookups)).toThrowError(/active category/);
    expect(() => catalogDraftsFromCsv('name,slug,category,sku,tenantId\nItem,item,spices,one,foreign', lookups)).toThrowError(/template columns/);
    expect(() => readCatalogCsv('name\n"not closed')).toThrowError(/not closed/);
    expect(() => catalogDraftsFromCsv('name,slug,category,sku\nFirst,first,spices,sku\nSecond,second,spices,SKU', lookups)).toThrowError(/different/);
  });
  it('bounds batch size and rejects executable-looking financial columns instead of importing client prices', () => {
    expect(() => catalogDraftsFromCsv('name,slug,category,sku,price\nItem,item,spices,sku,0.01', lookups)).toThrowError(/template columns/);
    expect(() => readCatalogCsv('name\n' + Array.from({length:101},(_,i)=>'row'+i).join('\n'))).toThrowError(/100 products/);
  });
});
