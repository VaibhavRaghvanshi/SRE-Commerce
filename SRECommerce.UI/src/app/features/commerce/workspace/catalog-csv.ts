import { CatalogEditor, MerchantLookups } from '../../../core/services/merchant-catalog.service';

const columns = ['name', 'slug', 'category', 'brand', 'sku', 'description', 'barcode', 'visible', 'b2bonly', 'attributes', 'specifications'];
export function readCatalogCsv(text: string): string[][] {
  const rows: string[][] = []; let row: string[] = [], cell = '', quoted = false, closed = false;
  function field() { row.push(cell); cell = ''; closed = false; if (row.length > columns.length) throw new Error('Too many CSV columns.'); }
  function record() { field(); if (row.some(value => value.trim())) rows.push(row); row = []; if (rows.length > 101) throw new Error('Import up to 100 products at a time.'); }
  text = text.replace(/^\uFEFF/, '');
  for (let i = 0; i < text.length; i++) {
    const char = text[i];
    if (quoted) { if (char === '"') { if (text[i + 1] === '"') { cell += '"'; i++; } else { quoted = false; closed = true; } } else cell += char; continue; }
    if (char === ',') { field(); continue; }
    if (char === '\r' || char === '\n') { if (char === '\r' && text[i + 1] === '\n') i++; record(); continue; }
    if (closed) throw new Error('Unexpected text after a quoted CSV field.');
    if (char === '"') { if (cell) throw new Error('Quotes must enclose the whole CSV field.'); quoted = true; } else cell += char;
  }
  if (quoted) throw new Error('A quoted CSV field was not closed.');
  if (cell || row.length || closed) record(); return rows;
}
export function catalogDraftsFromCsv(text: string, lookups: MerchantLookups): CatalogEditor[] {
  const [header, ...rows] = readCatalogCsv(text); if (!header || !rows.length) throw new Error('Choose a CSV file with a header and 1–100 products.');
  const names = header.map(value => value.trim().toLowerCase());
  if (new Set(names).size !== names.length || names.some(value => !columns.includes(value)) || ['name', 'slug', 'category', 'sku'].some(value => !names.includes(value))) throw new Error('Use the template columns. Name, slug, category and SKU are required.');
  const slugs = new Set<string>(), skus = new Set<string>();
  return rows.map((cells, index) => {
    if (cells.length !== header.length) throw new Error('CSV row ' + (index + 2) + ' has the wrong number of columns.');
    const row = Object.fromEntries(names.map((name, i) => [name, cells[i].trim()]));
    const category = lookups.categories.find(item => !item.archived && item.slug === row['category'].toLowerCase());
    const brand = row['brand'] ? lookups.brands.find(item => !item.archived && item.slug === row['brand'].toLowerCase()) : null;
    if (!category || row['brand'] && !brand) throw new Error('CSV row ' + (index + 2) + ' needs an active category and, if supplied, an active brand slug.');
    if (!row['name'] || !row['sku'] || !/^[a-z0-9]+(-[a-z0-9]+)*$/.test(row['slug'])) throw new Error('CSV row ' + (index + 2) + ' needs a name, a lowercase URL slug and a SKU.');
    const sku = row['sku'].toUpperCase(); if (slugs.has(row['slug']) || skus.has(sku)) throw new Error('Every product slug and SKU in this file must be different.'); slugs.add(row['slug']); skus.add(sku);
    function boolean(name: string, fallback: boolean) { const value = row[name]?.toLowerCase(); if (!value) return fallback; if (!['true', 'false'].includes(value)) throw new Error('Use true or false for ' + name + ' in row ' + (index + 2) + '.'); return value === 'true'; }
    function object(name: string) {
      let value: unknown; try { value = JSON.parse(row[name] || '{}'); } catch { throw new Error('Invalid ' + name + ' JSON in row ' + (index + 2) + '.'); }
      if (!value || typeof value !== 'object' || Array.isArray(value) || Object.keys(value).length > 60) throw new Error('Use a JSON object with up to 60 entries for ' + name + '.');
      return value as Record<string, unknown>;
    }
    const attributes = object('attributes'), specifications = object('specifications');
    if (Object.values(attributes).some(value => !['string', 'number', 'boolean'].includes(typeof value) || typeof value === 'number' && !Number.isFinite(value)) || Object.values(specifications).some(value => typeof value !== 'string')) throw new Error('Attributes must be text, finite numbers or booleans; specification values must be text.');
    return { name: row['name'], slug: row['slug'], categoryId: category.id, brandId: brand?.id || null, kind: 0, description: row['description'] || '', seoTitle: '', seoDescription: '', visible: boolean('visible', true), b2BOnly: boolean('b2bonly', false),
      attributes: attributes as CatalogEditor['attributes'], specifications: specifications as CatalogEditor['specifications'], variants: [{ id: null, sku, options: {}, barcode: row['barcode'] || null, active: true }], media: [], bundleItems: [], relationships: [], version: null };
  });
}
export function catalogCsvTemplate(category: string): string {
  const quote = (value: string) => '"' + value.replace(/"/g, '""') + '"';
  return [columns.join(','), ['New product', 'new-product', category, '', 'NEW-PRODUCT-001', 'Describe your product', '', 'true', 'false', '{}', '{}'].map(quote).join(',')].join('\r\n') + '\r\n';
}
