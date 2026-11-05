import { existsSync, readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { createRequire } from 'node:module';
import { request, signIn } from './lib/local-commerce.mjs';

// Owner membership exercises the private audience without creating runtime accounts.
// Actual B2BBuyer/Customer role separation is covered by the isolated SQL/API suite.
if (!process.argv.includes('--apply')) {
  console.log(JSON.stringify({ dryRun: true, scope: 'labelled local B2B product, private image, normal buyer PKCE, wishlist and logout' }));
  process.exit(0);
}
const require = createRequire(new URL('../SRECommerce.UI/package.json', import.meta.url));
const { chromium } = require('playwright-core');
const chrome = ['C:/Program Files/Google/Chrome/Application/chrome.exe', 'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe'].find(existsSync);
const owner = JSON.parse(readFileSync(join(process.env.APPDATA, 'Microsoft/UserSecrets/SRECommerce-LegacyDemo-Development/secrets.json'), 'utf8'));
const base = { ui: 'http://localhost:4000', identity: 'http://localhost:5181', tenant: 'http://localhost:5182', catalog: 'http://localhost:5185', cart: 'http://localhost:5188' };
const root = join(process.cwd(), 'artifacts/buyer-catalog-browser'); mkdirSync(root, { recursive: true });
const tenant = owner.TenantId, stamp = Date.now().toString(), slug = 'buyer-check-' + stamp, name = 'Buyer verification ' + stamp;
const cp = '/api/v1/tenants/' + tenant + '/catalog', account = '/api/v1/tenants/' + tenant + '/cart/account';
const checks = []; let browser, token, product, step = 'preflight';
function assert(value, message) { if (!value) throw new Error(message); }
function record(check) { checks.push(check); console.log(JSON.stringify({ passed: check })); }
try {
  const registry = await (await request(base.tenant, '/api/v1/public/tenants/legacy-demo')).json();
  assert(registry.tenantId === tenant, 'Wrong development store');
  token = await signIn(base.identity, owner.OwnerEmail, owner.OwnerPassword, tenant);
  const lookups = await (await request(base.catalog, cp + '/lookups', { token })).json();
  const category = lookups.categories.find(row => !row.archived); assert(category, 'No active category');
  const multipart = new FormData(); multipart.append('file', new Blob([Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a0wAAAABJRU5ErkJggg==', 'base64')], { type: 'image/png' }), 'buyer-verification.png');
  const upload = await fetch(base.catalog + cp + '/media-assets', { method: 'POST', headers: { Authorization: 'Bearer ' + token }, body: multipart });
  assert(upload.ok, 'Scoped image upload failed'); const asset = await upload.json();
  const draft = { name, slug, categoryId: category.id, brandId: null, kind: 0, description: 'Synthetic private buyer verification', seoTitle: '', seoDescription: '', visible: true, b2BOnly: true,
    attributes: {}, specifications: {}, variants: [{ id: null, sku: 'BUYER-CHECK-' + stamp, options: {}, barcode: null, active: true }],
    media: [{ id: null, url: asset.url, alt: name, order: 0 }], bundleItems: [], relationships: [], version: null };
  product = await (await request(base.catalog, cp + '/products', { token, method: 'POST', body: draft, status: 201 })).json();
  product = await (await request(base.catalog, cp + '/products/' + product.id + '/publish', { token, method: 'POST', body: { version: product.version } })).json();
  step = 'public audience isolation';
  await request(base.catalog, '/api/v1/public/tenants/' + tenant + '/catalog/products/' + slug, { status: 404 });
  assert((await fetch(base.ui + asset.url)).status === 404, 'B2B image was exposed publicly');
  await request(base.catalog, cp + '/buyer/media/' + asset.id, { status: 401 });
  record('anonymous product and managed image access denied');
  browser = await chromium.launch({ headless: true, executablePath: chrome });
  const page = await browser.newPage({ viewport: { width: 1365, height: 900 } }); page.setDefaultTimeout(30000);
  await page.goto(base.ui + '/stores/legacy-demo');
  step = 'buyer PKCE';
  await page.getByRole('button', { name: 'Business buyer sign in', exact: true }).click();
  await page.locator('input[name="email"]').fill(owner.OwnerEmail); await page.locator('input[name="password"]').fill(owner.OwnerPassword);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.getByText('Member catalog', { exact: false }).first().waitFor();
  await page.locator('input[name="search"]').fill(name);
  await page.locator('form.catalog-filters').getByRole('button', { name: 'Search', exact: true }).click();
  await page.locator('.product-grid h3').getByRole('link', { name, exact: true }).waitFor(); record('buyer sign-in returns to a membership-filtered storefront');
  step = 'private product image';
  await page.locator('.product-grid h3').getByRole('link', { name, exact: true }).click();
  await page.getByRole('heading', { name, exact: true }).waitFor();
  await page.waitForFunction(() => { const image = document.querySelector('.images img'); return image?.src.startsWith('blob:') && image.complete && image.naturalWidth > 0; });
  record('private product detail loads its authenticated image into a memory URL');
  step = 'private wishlist';
  await page.getByRole('button', { name: 'Save to wishlist', exact: true }).click();
  await page.getByText('Saved to your wishlist.', { exact: true }).waitFor();
  const wishlist = await (await request(base.cart, account + '/products/0', { token })).json();
  assert(wishlist.items.some(item => item.productId === product.id && item.available && item.name === name), 'Private wishlist did not reconcile');
  record('wishlist reconciles only eligible published buyer products');
  await page.screenshot({ path: join(root, 'buyer-detail.png'), fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Buyer mobile page overflow');
  record('buyer detail adapts to mobile width');
  step = 'logout isolation';
  await page.getByRole('link', { name: /Back to/ }).click();
  await page.getByRole('button', { name: 'Sign out', exact: true }).click();
  await page.getByRole('button', { name: 'Business buyer sign in', exact: true }).waitFor();
  assert(await page.locator('img[src^="blob:"]').count() === 0, 'Private image remained after logout');
  await page.goto(base.ui + '/stores/legacy-demo/products/' + slug);
  await page.getByText('This product is unavailable or has not been published.', { exact: true }).waitFor();
  record('logout removes private catalog access and in-memory image URLs');
} catch (error) {
  process.exitCode = 1;
  const page = browser?.contexts()[0]?.pages()[0]; if (page) try { await page.screenshot({ path: join(root, 'failure.png'), fullPage: true }); } catch {}
  console.error(JSON.stringify({ failedStep: step, errorType: error.name, privateDetailsOmitted: true }));
} finally {
  const cleanup = [];
  if (token && product) try {
    for (const kind of [0, 1]) {
      const saved = await (await request(base.cart, account + '/products/' + kind, { token })).json();
      const item = saved.items.find(row => row.productId === product.id);
      if (item) await request(base.cart, account + '/products/' + kind + '/' + product.id, { token, method: 'PUT', body: { active: false, version: item.version } });
    }
    const fresh = await (await request(base.catalog, cp + '/products/' + product.id, { token })).json();
    assert(fresh.slug === slug, 'Owned product identity changed');
    await request(base.catalog, cp + '/products/' + product.id + '/archive', { token, method: 'POST', body: { version: fresh.version }, status: 204 });
    cleanup.push('only own labelled product archived and saved-product references removed; media preserved');
  } catch { cleanup.push('own labelled resources require review'); process.exitCode = 1; }
  if (browser) await browser.close();
  writeFileSync(join(root, 'result.json'), JSON.stringify({ checks, cleanup, success: !process.exitCode }, null, 2));
  console.log(JSON.stringify({ checks: checks.length, cleanup, success: !process.exitCode }));
}
