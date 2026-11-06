import { existsSync, readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { createRequire } from 'node:module';
import { request, signIn } from './lib/local-commerce.mjs';

if (!process.argv.includes('--apply')) { console.log(JSON.stringify({ applyRequired: true, scope: 'own labelled category/field and one draft; real import response-loss/replay and JSON download; soft-archive own fixtures' })); process.exit(0); }
const require = createRequire(new URL('../SRECommerce.UI/package.json', import.meta.url));
const { chromium } = require('playwright-core');
const chrome = ['C:/Program Files/Google/Chrome/Application/chrome.exe', 'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe'].find(existsSync);
const owner = JSON.parse(readFileSync(join(process.env.APPDATA, 'Microsoft/UserSecrets/SRECommerce-LegacyDemo-Development/secrets.json'), 'utf8'));
const mapping = JSON.parse(readFileSync('artifacts/legacy-demo-catalog-mapping.json', 'utf8'));
if (mapping.tenantId !== owner.TenantId) throw new Error('DedicatedDemoTenantMismatch');
const tenant = owner.TenantId, cp = '/api/v1/tenants/' + tenant + '/catalog', stamp = Date.now().toString();
const slug = 'csv-review-' + stamp, name = 'CSV review ' + stamp, sku = 'CSV-REVIEW-' + stamp, fieldCode = 'csv-field-' + stamp;
const directory = 'artifacts/catalog-transfer-browser'; mkdirSync(directory, { recursive: true });
const checks = [], cleanup = []; let token, browser, page, category, field, step = 'preflight';
const ids = []; let firstBatchKey = null;
function assert(value, code) { if (!value) throw new Error(code); }
function record(check) { checks.push(check); console.log(JSON.stringify({ passed: check })); }
const get = async path => (await request('http://localhost:5185', cp + path, { token })).json();
try {
  token = await signIn('http://localhost:5181', owner.OwnerEmail, owner.OwnerPassword, tenant);
  category = await (await request('http://localhost:5185', cp + '/categories', { token, method: 'POST', status: 201, body: { name, slug, parentId: null, version: null, archived: false } })).json();
  browser = await chromium.launch({ executablePath: chrome, headless: true }); page = await browser.newPage({ viewport: { width: 1365, height: 900 } }); page.setDefaultTimeout(45000);
  await page.goto('http://localhost:4000/workspace/' + tenant + '/catalog');
  await page.getByRole('button', { name: 'Secure sign in', exact: true }).click();
  await page.locator('input[name=email]').fill(owner.OwnerEmail); await page.locator('input[name=password]').fill(owner.OwnerPassword);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click(); await page.getByRole('button', { name: 'New product', exact: true }).waitFor();
  await page.locator('summary').filter({ hasText: 'Catalog setup & bulk tools' }).click();
  const fields = page.locator('app-catalog-fields'), transfer = page.locator('app-catalog-transfer');
  step = 'field creation'; await fields.locator('[name=field-name]').fill(name); await fields.locator('[name=field-code]').fill(fieldCode);
  const created = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith(cp.replace('/api/v1', '') + '/attributes'));
  await fields.getByRole('button', { name: 'Create field', exact: true }).click(); const fieldResponse = await created; assert(fieldResponse.status() === 201, 'FieldCreateRejected'); field = await fieldResponse.json();
  await fields.getByText('Product field saved.', { exact: true }).waitFor(); record('merchant creates a versioned field through the live gateway');
  step = 'bound field protection';
  const categoryFields = page.waitForResponse(r => r.request().method() === 'GET' && new URL(r.url()).pathname.endsWith('/categories/' + category.id + '/attributes'));
  await page.locator('[name=binding-category]').selectOption(category.id); assert((await categoryFields).status() === 200, 'CategoryFieldReadRejected');
  await page.getByRole('checkbox', { name, exact: true }).check();
  const bind = () => page.waitForResponse(r => r.request().method() === 'PUT' && new URL(r.url()).pathname.endsWith('/categories/' + category.id + '/attributes'));
  let bindingResponse = bind(); await page.getByRole('button', { name: 'Save category fields', exact: true }).click(); assert((await bindingResponse).status() === 204, 'BindingRejected');
  await fields.locator('[name=field-selection]').selectOption(field.id); await fields.locator('[name=field-archived]').check();
  await fields.getByRole('button', { name: 'Save field', exact: true }).click();
  await fields.getByText('Unassign this field from all categories before archiving it.', { exact: true }).waitFor();
  record('archiving a bound field is rejected with actionable UI feedback');
  step = 'field unbind and archive';
  await page.getByRole('checkbox', { name, exact: true }).uncheck(); bindingResponse = bind();
  await page.getByRole('button', { name: 'Save category fields', exact: true }).click(); assert((await bindingResponse).status() === 204, 'UnbindingRejected');
  await fields.getByRole('button', { name: 'Save field', exact: true }).click(); await fields.getByText('Product field saved.', { exact: true }).waitFor();
  assert((await get('/lookups')).definitions.some(row => row.id === field.id && row.archived), 'FieldWasNotArchived'); record('explicit unbinding permits versioned field archival');
  step = 'CSV preview and lost response';
  const csv = 'name,slug,category,sku,description\r\n"' + name + '",' + slug + ',' + slug + ',' + sku + ',"A labelled import, with a quoted comma"\r\n';
  await transfer.locator('input[type=file]').setInputFiles({ name: 'review.csv', mimeType: 'text/csv', buffer: Buffer.from(csv) });
  await transfer.getByRole('heading', { name: 'Review 1 new drafts', exact: true }).waitFor();
  let loseResponse = true;
  await page.route('**/commerce/catalog/tenants/' + tenant + '/catalog/import', async route => {
    const body = route.request().postDataJSON();
    if (firstBatchKey) assert(body.key === firstBatchKey, 'ImportRetryKeyChanged'); else firstBatchKey = body.key;
    const response = await route.fetch();
    if (response.ok()) { const result = await response.json(); ids.push(...result); }
    if (loseResponse && response.ok()) { loseResponse = false; await route.abort('failed'); } else await route.fulfill({ response });
  });
  await transfer.getByRole('button', { name: 'Create 1 drafts', exact: true }).click();
  await transfer.getByRole('button', { name: 'Retry the same batch', exact: true }).waitFor();
  assert(await transfer.getByRole('button', { name: 'Discard preview', exact: true }).isDisabled(), 'UncertainBatchWasDiscardable');
  await transfer.getByRole('button', { name: 'Retry the same batch', exact: true }).click(); await transfer.getByText('1 new drafts created. Review each product before publishing.', { exact: true }).waitFor();
  assert(ids.length === 2 && ids[0] === ids[1], 'RealImportReplayCreatedDifferentProducts');
  const imported = await get('/products?search=' + encodeURIComponent(slug));
  assert(imported.total === 1 && imported.items[0].id === ids[0] && imported.items[0].publishedRevision === 0, 'ImportWasNotExactlyOneDraft');
  record('a real committed import with a lost response replays the same key and creates exactly one unpublished draft');
  step = 'merchant search'; await page.locator('[name=catalog-search]').fill(slug); await page.getByRole('button', { name: 'Search products', exact: true }).click();
  await page.locator('.product-table tbody tr').filter({ hasText: name }).waitFor(); assert(await page.locator('.product-table tbody tr').count() === 1, 'ScopedSearchDidNotFilter'); record('merchant product search finds the imported SQL draft');
  step = 'JSON export'; const download = page.waitForEvent('download', { timeout: 120000 });
  await transfer.getByRole('button', { name: 'Download catalog JSON', exact: true }).click(); const file = await download;
  const exportPath = join(directory, 'catalog-export.json'); await file.saveAs(exportPath); const snapshot = JSON.parse(readFileSync(exportPath, 'utf8'));
  assert(snapshot.schemaVersion === 1 && snapshot.tenantId === tenant && snapshot.products.filter(row => row.id === ids[0]).length === 1, 'ExportSnapshotMismatch');
  assert(Object.values(mapping.products).every(id => snapshot.products.some(row => row.id === id)), 'OriginalsMissingFromExport'); record('the downloaded paged JSON includes the own draft and all 1000 preserved originals');
  step = 'archive confirmation';
  await page.locator('.product-table tbody tr').filter({ hasText: name }).getByRole('button', { name: 'Archive', exact: true }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Keep product', exact: true }).click(); assert((await get('/products/' + ids[0])).status !== 2, 'ArchiveCancelChangedProduct');
  await page.locator('.product-table tbody tr').filter({ hasText: name }).getByRole('button', { name: 'Archive', exact: true }).click(); await page.getByRole('dialog').getByRole('button', { name: 'Archive product', exact: true }).click();
  await page.getByText('Product archived. Its history is preserved.', { exact: true }).waitFor(); assert((await get('/products/' + ids[0])).status === 2, 'ConfirmedArchiveFailed');
  record('archive dialog cancellation preserves the draft and explicit confirmation archives only the labelled fixture');
  await page.screenshot({ path: join(directory, 'catalog-tools.png'), fullPage: true });
} catch (error) {
  process.exitCode = 1; if (page) try { await page.screenshot({ path: join(directory, 'failure.png'), fullPage: true }); } catch {}
  console.error(JSON.stringify({ step, failureType: /^[A-Za-z]+$/.test(error.message || '') ? error.message : error.name, privateDetailsOmitted: true }));
} finally {
  if (category) try {
    token = await signIn('http://localhost:5181', owner.OwnerEmail, owner.OwnerPassword, tenant);
    const products = await get('/products?search=' + encodeURIComponent(slug));
    for (const product of products.items.filter(row => row.slug === slug && row.name === name && row.variants.length === 1 && row.variants[0].sku === sku))
      if (product.status !== 2) await request('http://localhost:5185', cp + '/products/' + product.id + '/archive', { token, method: 'POST', body: { version: product.version }, status: 204 });
    let lookups = await get('/lookups'), own = lookups.categories.find(row => row.id === category.id && row.slug === slug); assert(own, 'CleanupCategoryOwnershipMismatch');
    await request('http://localhost:5185', cp + '/categories/' + own.id + '/attributes?version=' + encodeURIComponent(own.version), { token, method: 'PUT', body: [], status: 204 });
    lookups = await get('/lookups'); const ownField = lookups.definitions.find(row => row.code === fieldCode);
    if (ownField && !ownField.archived) await request('http://localhost:5185', cp + '/attributes/' + ownField.id, { token, method: 'PUT', body: { code: ownField.code, name: ownField.name, dataType: ownField.dataType, options: ownField.options, unit: ownField.unit, archived: true, version: ownField.version } });
    own = lookups.categories.find(row => row.id === category.id && row.slug === slug);
    await request('http://localhost:5185', cp + '/categories/' + own.id, { token, method: 'PUT', body: { name: own.name, slug: own.slug, parentId: null, archived: true, version: own.version } });
    cleanup.push('own labelled draft, field and category soft-archived; original products/data preserved');
  } catch { process.exitCode = 1; cleanup.push('own labelled fixtures require review; no rows deleted'); }
  if (browser) await browser.close(); writeFileSync(join(directory, 'result.json'), JSON.stringify({ checks, cleanup, success: !process.exitCode }, null, 2));
  console.log(JSON.stringify({ checks: checks.length, cleanup, success: !process.exitCode }));
}
