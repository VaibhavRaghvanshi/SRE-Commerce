import { existsSync, readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { createRequire } from 'node:module';
import { request, signIn } from './lib/local-commerce.mjs';

if (!process.argv.includes('--apply')) { console.log(JSON.stringify({ dryRun: true, scope: 'labelled Legacy Demo category/brand/collection verification' })); process.exit(0); }
const require = createRequire(new URL('../SRECommerce.UI/package.json', import.meta.url));
const { chromium } = require('playwright-core');
const chrome = ['C:/Program Files/Google/Chrome/Application/chrome.exe', 'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe'].find(existsSync);
const owner = JSON.parse(readFileSync(join(process.env.APPDATA, 'Microsoft/UserSecrets/SRECommerce-LegacyDemo-Development/secrets.json'), 'utf8'));
const base = { ui: 'http://localhost:4000', identity: 'http://localhost:5181', tenant: 'http://localhost:5182', catalog: 'http://localhost:5185' };
const root = join(process.cwd(), 'artifacts/catalog-organization-browser'); mkdirSync(root, { recursive: true });
const stamp = Date.now().toString(), cp = '/api/v1/tenants/' + owner.TenantId + '/catalog';
const names = { parent: 'Org parent ' + stamp, child: 'Org child ' + stamp, brand: 'Org brand ' + stamp, collection: 'Org collection ' + stamp };
const resources = { parent: null, child: null, brand: null, collection: null, products: [] }, checks = [];
let browser, token, step = 'preflight';
function assert(value, message) { if (!value) throw new Error(message); }
function record(check) { checks.push(check); console.log(JSON.stringify({ passed: check })); }
try {
  const registry = await (await request(base.tenant, '/api/v1/public/tenants/legacy-demo')).json(); assert(registry.tenantId === owner.TenantId, 'Wrong store');
  token = await signIn(base.identity, owner.OwnerEmail, owner.OwnerPassword, owner.TenantId);
  await request(base.identity, '/api/v1/me/access', { token }); await request(base.catalog, cp + '/lookups', { token });
  browser = await chromium.launch({ headless: true, executablePath: chrome });
  const page = await browser.newPage({ viewport: { width: 1365, height: 900 } }); page.setDefaultTimeout(30000);
  await page.goto(base.ui + '/workspace/' + owner.TenantId + '/catalog');
  await page.getByRole('button', { name: 'Secure sign in', exact: true }).click();
  await page.locator('input[name="email"]').fill(owner.OwnerEmail); await page.locator('input[name="password"]').fill(owner.OwnerPassword);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.locator('summary').filter({ hasText: 'Catalog setup & bulk tools' }).click();
  const org = page.locator('app-catalog-organization'); await org.getByRole('heading', { name: 'Organize your catalog', exact: true }).waitFor();
  const response = (method, suffix) => page.waitForResponse(r => r.request().method() === method && r.url().endsWith('/catalog/' + suffix));
  async function save(button, method, suffix) { const pending = response(method, suffix); await org.getByRole('button', { name: button, exact: true }).click(); const r = await pending; assert(r.ok(), 'Organization write failed'); return r.json(); }
  step = 'category hierarchy';
  await org.locator('[name="category-name"]').fill(names.parent); await org.locator('[name="category-slug"]').fill('org-parent-' + stamp);
  resources.parent = await save('Create category', 'POST', 'categories');
  await org.locator('[name="category-selection"]').selectOption('');
  await org.locator('[name="category-name"]').fill(names.child); await org.locator('[name="category-slug"]').fill('org-child-' + stamp);
  await org.locator('[name="parent-category"]').selectOption({ label: names.parent });
  resources.child = await save('Create category', 'POST', 'categories'); assert(resources.child.parentId === resources.parent.id, 'Parent category mismatch');
  record('category hierarchy saved through versioned API');
  step = 'brand create and edit';
  await org.locator('[name="brand-name"]').fill(names.brand); await org.locator('[name="brand-slug"]').fill('org-brand-' + stamp);
  resources.brand = await save('Create brand', 'POST', 'brands');
  await org.locator('[name="brand-name"]').fill(names.brand + ' updated'); resources.brand = await save('Save brand', 'PUT', 'brands/' + resources.brand.id);
  assert(resources.brand.name === names.brand + ' updated', 'Brand edit missing'); record('brand create and edit preserve row versions');
  step = 'labelled products';
  for (const [key, name] of [['alpha', 'Org Alpha '], ['zulu', 'Org Zulu ']]) {
    const draft = { name: name + stamp, slug: 'org-' + key + '-' + stamp, categoryId: resources.child.id, brandId: resources.brand.id, kind: 0,
      description: 'Synthetic local organization verification', seoTitle: '', seoDescription: '', visible: true, b2BOnly: false,
      attributes: {}, specifications: {}, variants: [{ id: null, sku: 'ORG-' + key.toUpperCase() + '-' + stamp, options: {}, barcode: null, active: true }], media: [], bundleItems: [], relationships: [], version: null };
    const product = await (await request(base.catalog, cp + '/products', { token, method: 'POST', body: draft, status: 201 })).json(); resources.products.push(product);
    resources.products[resources.products.length - 1] = await (await request(base.catalog, cp + '/products/' + product.id + '/publish', { token, method: 'POST', body: { version: product.version } })).json();
  }
  step = 'ordered collection';
  await org.locator('[name="collection-name"]').fill(names.collection); await org.locator('[name="collection-slug"]').fill('org-collection-' + stamp);
  await org.locator('[name="product-query"]').fill(stamp); await org.getByRole('button', { name: 'Find products', exact: true }).click();
  for (const product of [...resources.products].reverse()) await org.locator('.results article').filter({ hasText: product.name }).getByRole('button', { name: 'Add to collection', exact: true }).click();
  resources.collection = await save('Create collection', 'POST', 'collections');
  const publicProducts = async () => (await (await request(base.catalog, '/api/v1/public/tenants/' + owner.TenantId + '/catalog/products?collection=' + resources.collection.slug)).json()).items;
  assert(JSON.stringify((await publicProducts()).map(p => p.id)) === JSON.stringify([...resources.products].reverse().map(p => p.id)), 'Configured order was ignored');
  await org.getByRole('button', { name: 'Move product 2 up', exact: true }).click(); resources.collection = await save('Save collection', 'PUT', 'collections/' + resources.collection.id);
  assert(JSON.stringify((await publicProducts()).map(p => p.id)) === JSON.stringify(resources.products.map(p => p.id)), 'Saved reorder was ignored'); record('collection picker and storefront keep configured product order');
  step = 'collection label batch';
  await org.locator('[name="collection-selection"]').selectOption('');
  const lookup = response('POST', 'product-choices'); await org.locator('[name="collection-selection"]').selectOption(resources.collection.id);
  const labels = await lookup; assert(labels.ok() && (await labels.json()).length === 2, 'Collection label batch failed');
  await org.locator('.members li').filter({ hasText: resources.products[0].name }).waitFor(); record('saved collection reload resolves scoped product labels');
  step = 'brand archive and restore';
  await org.locator('[name="brand-archived"]').check(); resources.brand = await save('Save brand', 'PUT', 'brands/' + resources.brand.id);
  assert((await publicProducts()).length === 0, 'Archived brand still visible');
  await org.locator('[name="brand-archived"]').uncheck(); resources.brand = await save('Save brand', 'PUT', 'brands/' + resources.brand.id);
  assert((await publicProducts()).length === 2, 'Restored brand missing'); record('archive/restore changes public eligibility without deleting data');
  await org.scrollIntoViewIfNeeded(); await page.screenshot({ path: join(root, 'organization-desktop.png'), fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 }); assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Mobile page overflow');
  await page.screenshot({ path: join(root, 'organization-mobile.png'), fullPage: true }); record('organization forms adapt to mobile width');
} catch (error) {
  process.exitCode = 1; const page = browser?.contexts()[0]?.pages()[0]; if (page) try { await page.screenshot({ path: join(root, 'failure.png'), fullPage: true }); } catch {}
  console.error(JSON.stringify({ failedStep: step, errorType: error.name, privateDetailsOmitted: true }));
} finally {
  const cleanup = [];
  if (token) {
    try {
      const current = await (await request(base.catalog, cp + '/lookups', { token })).json();
      for (const [key, field, slug] of [['collection', 'collections', 'org-collection-'], ['brand', 'brands', 'org-brand-'], ['child', 'categories', 'org-child-'], ['parent', 'categories', 'org-parent-']])
        resources[key] = current[field].find(row => row.slug === slug + stamp) || resources[key];
      const currentProducts = await (await request(base.catalog, cp + '/products?search=' + stamp, { token })).json();
      resources.products = currentProducts.items.filter(p => ['org-alpha-' + stamp, 'org-zulu-' + stamp].includes(p.slug));
      for (const p of resources.products) {
        const fresh = await (await request(base.catalog, cp + '/products/' + p.id, { token })).json();
        if (!['org-alpha-' + stamp, 'org-zulu-' + stamp].includes(fresh.slug)) throw new Error('Owned product identity changed');
        if (fresh.status !== 2) await request(base.catalog, cp + '/products/' + p.id + '/archive', { token, method: 'POST', body: { version: fresh.version }, status: 204 });
      }
      for (const [key, field] of [['collection', 'collections'], ['brand', 'brands'], ['child', 'categories'], ['parent', 'categories']]) {
        const row = resources[key]; if (!row) continue;
        const body = { name: row.name, slug: row.slug, version: row.version, archived: true };
        if (field === 'categories') body.parentId = row.parentId || null;
        if (field === 'collections') body.productIds = row.productIds;
        await request(base.catalog, cp + '/' + field + '/' + row.id, { token, method: 'PUT', body });
      }
      cleanup.push('only own labelled products, collection, brand and category hierarchy archived');
    } catch { cleanup.push('own labelled resource cleanup requires review'); process.exitCode = 1; }
  }
  if (browser) await browser.close(); writeFileSync(join(root, 'result.json'), JSON.stringify({ checks, cleanup, success: !process.exitCode }, null, 2));
  console.log(JSON.stringify({ checks: checks.length, cleanup, success: !process.exitCode }));
}
