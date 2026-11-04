import { readFileSync, mkdirSync, existsSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { createRequire } from 'node:module';
import { request, signIn } from './lib/local-commerce.mjs';

// Explicit local verification: creates only labelled development records, then archives
// its own address/product. It never charges money, sends email or publishes the app.
if (!process.argv.includes('--apply')) {
  console.log(JSON.stringify({ dryRun: true, store: 'legacy-demo', checks: ['PKCE', 'image upload/publication', 'wishlist', 'addresses', 'recent products', 'sales report', 'mobile layout'] }));
  process.exit(0);
}
const base = { ui: 'http://localhost:4000', identity: 'http://localhost:5181', tenant: 'http://localhost:5182', catalog: 'http://localhost:5185', cart: 'http://localhost:5188', order: 'http://localhost:5189' };
const require = createRequire(new URL('../SRECommerce.UI/package.json', import.meta.url));
const { chromium } = require('playwright-core');
const chrome = ['C:/Program Files/Google/Chrome/Application/chrome.exe', 'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe'].find(existsSync);
if (!chrome) throw new Error('Local Chrome executable unavailable.');
const directory = join(process.cwd(), 'artifacts', 'customer-account-browser'); mkdirSync(directory, { recursive: true });
const registry = await (await request(base.tenant, '/api/v1/public/tenants/legacy-demo')).json();
const owner = JSON.parse(readFileSync(join(process.env.APPDATA, 'Microsoft/UserSecrets/SRECommerce-LegacyDemo-Development/secrets.json'), 'utf8'));
if (registry.slug !== 'legacy-demo' || registry.tenantId !== owner.TenantId || !['catalog', 'cart', 'orders'].every(m => registry.modules.includes(m)))
  throw new Error('Expected enabled dedicated Legacy Demo modules.');
const tenant = registry.tenantId, stamp = Date.now().toString(), name = 'Browser account verification ' + stamp, slug = 'account-check-' + stamp;
const cp = '/api/v1/tenants/' + tenant + '/catalog', account = '/api/v1/tenants/' + tenant + '/cart/account';
const checks = []; let step = 'start', product = null, address = null, wish = null, token = null, browser = null;
function assert(condition, message) { if (!condition) throw new Error(message); }
async function record(label) { checks.push(label); console.log(JSON.stringify({ passed: label })); }
try {
  token = await signIn(base.identity, owner.OwnerEmail, owner.OwnerPassword, tenant);
  browser = await chromium.launch({ headless: true, executablePath: chrome });
  const context = await browser.newContext({ viewport: { width: 1365, height: 900 } });
  const page = await context.newPage(); page.setDefaultTimeout(30000);
  step = 'customer PKCE';
  await page.goto(base.ui + '/stores/legacy-demo/account');
  await page.getByRole('button', { name: 'Sign in to this store', exact: true }).click();
  await page.locator('input[name="email"]').fill(owner.OwnerEmail);
  await page.locator('input[name="password"]').fill(owner.OwnerPassword);
  await page.locator('button[type="submit"]').click();
  await page.getByRole('heading', { name: 'Your addresses', exact: true }).waitFor();
  await record('normal Angular code+PKCE account sign-in');

  step = 'address editor';
  await page.getByRole('button', { name: 'Add address', exact: true }).click();
  await page.getByLabel('Label', { exact: true }).fill('Browser check ' + stamp);
  await page.getByLabel('Full name', { exact: true }).fill('Local browser verification');
  await page.getByLabel('Email', { exact: true }).fill('browser-check@example.test');
  await page.getByLabel('Street address', { exact: true }).fill('1 Synthetic Test Street');
  await page.getByLabel('City', { exact: true }).fill('Pune');
  await page.getByLabel('Country code', { exact: true }).fill('IN');
  await page.getByLabel('Postal code', { exact: true }).fill('411001');
  const saveAddress = page.waitForResponse(r => r.request().method() === 'PUT' && /\/cart\/account\/addresses\/[a-f0-9-]+$/.test(r.url()));
  await page.getByRole('button', { name: 'Save address', exact: true }).click();
  const addressResponse = await saveAddress; assert(addressResponse.ok(), 'Address API rejected labelled address'); address = await addressResponse.json();
  await page.getByText('Address saved.', { exact: true }).waitFor();
  await record('address editor persists through owning Cart SQL API');
  await page.setViewportSize({ width: 390, height: 844 });
  assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Account mobile layout overflows');
  await page.screenshot({ path: join(directory, 'account-mobile.png'), fullPage: true });
  await page.setViewportSize({ width: 1365, height: 900 });
  await record('mobile account layout');

  step = 'catalog editor';
  await page.goto(base.ui + '/workspace/' + tenant + '/catalog');
  await page.getByRole('button', { name: 'Secure sign in', exact: true }).click();
  await page.getByRole('button', { name: 'New product', exact: true }).waitFor();
  await page.getByRole('button', { name: 'New product', exact: true }).click();
  const editor = page.locator('section.editor');
  await editor.getByLabel('Name', { exact: true }).fill(name);
  await editor.getByLabel('URL slug', { exact: true }).fill(slug);
  const category = await editor.locator('select[name="category"] option').nth(1).getAttribute('value');
  assert(category, 'No development category available');
  await editor.locator('select[name="category"]').selectOption(category);
  await editor.getByLabel('SKU', { exact: true }).fill('ACCOUNT-CHECK-' + stamp);
  const upload = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/catalog/media-assets'));
  await editor.locator('input[type="file"]').setInputFiles({ name: 'verification.png', mimeType: 'image/png',
    buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a0wAAAABJRU5ErkJggg==', 'base64') });
  const uploaded = await upload; assert(uploaded.ok(), 'Media upload failed'); const asset = await uploaded.json();
  assert((await fetch(base.ui + asset.url)).status === 404, 'Unpublished image was served publicly');
  const savedProduct = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/catalog/products'));
  await editor.getByRole('button', { name: 'Save draft', exact: true }).click();
  const savedResponse = await savedProduct; assert(savedResponse.ok(), 'Draft save failed'); product = await savedResponse.json(); assert(product.slug === slug, 'Wrong labelled draft');
  const row = page.locator('.product-table tbody tr').filter({ hasText: name });
  const publication = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/products/' + product.id + '/publish'));
  await row.getByRole('button', { name: 'Publish', exact: true }).click();
  const published = await publication; assert(published.ok(), 'Product publication failed'); product = await published.json();
  const image = await fetch(base.ui + asset.url); assert(image.ok && image.headers.get('content-type')?.startsWith('image/png'), 'Published image MIME/read failed');
  await record('multipart image upload stays private until product publication');

  step = 'wishlist and recently viewed';
  await page.goto(base.ui + '/stores/legacy-demo/products/' + slug);
  await page.getByRole('heading', { name, exact: true }).waitFor();
  await page.waitForFunction(() => { const img = document.querySelector('.images img'); return img?.complete && img.naturalWidth > 0; });
  const customerSession = page.waitForResponse(r => r.url().endsWith('/commerce/identity/me/access'));
  await page.getByRole('button', { name: 'Save to wishlist', exact: true }).click();
  // Returning from the normal SSO callback requires the explicit save action again.
  assert((await customerSession).ok(), 'Customer SSO callback failed');
  await page.waitForURL(base.ui + '/stores/legacy-demo/products/' + slug);
  await page.getByRole('button', { name: 'Save to wishlist', exact: true }).waitFor();
  const saveWish = page.waitForResponse(r => r.request().method() === 'PUT' && r.url().endsWith('/products/0/' + product.id));
  await page.getByRole('button', { name: 'Save to wishlist', exact: true }).click();
  const wishResponse = await saveWish; assert(wishResponse.ok(), 'Wishlist save failed'); wish = await wishResponse.json();
  await page.getByText('Saved to your wishlist.', { exact: true }).waitFor();
  await page.getByRole('link', { name: 'Wishlist & recently viewed →', exact: true }).click();
  await page.getByRole('heading', { name: 'Your wishlist', exact: true }).waitFor();
  await page.getByRole('link', { name, exact: true }).first().waitFor();
  const recent = await (await request(base.cart, account + '/products/1', { token })).json();
  assert(recent.items.some(p => p.productId === product.id), 'Signed-in recent product missing');
  await record('wishlist and recent products use live public product identity');
  await page.screenshot({ path: join(directory, 'account-desktop.png'), fullPage: true });

  step = 'sales dashboard';
  await page.goto(base.ui + '/workspace/' + tenant + '/reports');
  await page.getByRole('button', { name: 'Sign in as owner or manager', exact: true }).click();
  await page.getByRole('heading', { name: 'Sales at a glance.', exact: true }).waitFor();
  await page.getByLabel('From', { exact: true }).fill(new Date(Date.now() - 365 * 86400000).toISOString().slice(0, 10));
  await page.getByLabel('Include imported history').check();
  const reportResponse = page.waitForResponse(r => r.url().includes('/orders/reports/sales?') && r.url().includes('includeHistorical=true'));
  await page.getByRole('button', { name: 'Refresh report', exact: true }).click();
  const report = await reportResponse; assert(report.ok(), 'Sales report API rejected query'); const reportData = await report.json();
  assert(reportData.currency === 'INR' && reportData.timeZone === 'UTC', 'Report currency/time-zone mismatch');
  assert(reportData.totals.historicalOrders >= 3, 'Imported order history missing from selected report');
  await page.getByRole('heading', { name: 'Daily accepted order value', exact: true }).waitFor();
  await page.screenshot({ path: join(directory, 'sales-desktop.png'), fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Sales mobile layout overflows');
  await page.screenshot({ path: join(directory, 'sales-mobile.png'), fullPage: true });
  await record('native Order SQL sales dashboard and mobile layout');
  const storage = await page.evaluate(() => JSON.stringify({ local: { ...localStorage }, session: { ...sessionStorage } }));
  assert(!storage.includes(owner.OwnerPassword) && !storage.includes('browser-check@example.test') && !storage.includes('access_token'), 'Sensitive account values persisted in browser storage');
  await record('no password, delivery contact or bearer token persisted in browser storage');
} catch (error) {
  const failedPage = browser?.contexts()[0]?.pages()[0];
  if (failedPage) try { await failedPage.screenshot({ path: join(directory, 'failure.png'), fullPage: true }); } catch { /* Preserve the original failure step. */ }
  process.exitCode = 1; console.error(JSON.stringify({ failedStep: step, errorType: error.name, privateDetailsOmitted: true }));
} finally {
  const cleanup = [];
  // A response may fail after a write commits. Reconcile only this run's unique
  // labels before cleanup; never clear a customer's whole address/wish list.
  if (token && !address) try {
    const rows = await (await request(base.cart, account + '/addresses', { token })).json();
    address = rows.find(row => row.label === 'Browser check ' + stamp && row.fullName === 'Local browser verification') || null;
  } catch { /* A failure remains recorded above; unrelated records are preserved. */ }
  if (token && !product && ['catalog editor', 'wishlist and recently viewed', 'sales dashboard'].includes(step)) try {
    const rows = await (await request(base.catalog, cp + '/products?search=' + encodeURIComponent(name), { token })).json();
    product = rows.items.find(row => row.slug === slug && row.name === name) || null;
  } catch { /* Do not guess a product identity when reconciliation is unavailable. */ }
  if (token && product && !wish) try {
    const rows = await (await request(base.cart, account + '/products/0', { token })).json();
    wish = rows.items.find(row => row.productId === product.id) || null;
  } catch { /* No unrelated wishlist entry is selected. */ }
  if (token && wish && product) try { await request(base.cart, account + '/products/0/' + product.id, { method: 'PUT', body: { active: false, version: wish.version }, token }); cleanup.push('own wishlist entry removed'); } catch { cleanup.push('own wishlist cleanup requires review'); process.exitCode = 1; }
  if (token && address) try { await request(base.cart, account + '/addresses/' + address.id + '/archive', { method: 'POST', body: { version: address.version }, token, status: 204 }); cleanup.push('own address archived'); } catch { cleanup.push('own address cleanup requires review'); process.exitCode = 1; }
  if (token && product) try {
    const current = await (await request(base.catalog, cp + '/products/' + product.id, { token })).json();
    assert(current.slug === slug, 'Owned product identity changed');
    await request(base.catalog, cp + '/products/' + product.id + '/archive', { method: 'POST', body: { version: current.version }, token, status: 204 }); cleanup.push('own labelled product archived');
  } catch { cleanup.push('own product cleanup requires review'); process.exitCode = 1; }
  if (browser) await browser.close();
  writeFileSync(join(directory, 'result.json'), JSON.stringify({ checks, cleanup, success: !process.exitCode }, null, 2));
  console.log(JSON.stringify({ checks: checks.length, cleanup, success: !process.exitCode }));
}
