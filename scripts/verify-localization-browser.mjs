import { existsSync, readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { createRequire } from 'node:module';
import { request, signIn } from './lib/local-commerce.mjs';

if (!process.argv.includes('--apply')) throw new Error('Use --apply to create and archive one labelled translated development product.');
const require = createRequire(new URL('../SRECommerce.UI/package.json', import.meta.url));
const { chromium } = require('playwright-core');
const executablePath = ['C:/Program Files/Google/Chrome/Application/chrome.exe', 'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe'].find(existsSync);
const config = JSON.parse(readFileSync(join(process.env.APPDATA, 'Microsoft/UserSecrets/SRECommerce-LegacyDemo-Development/secrets.json'), 'utf8'));
const directory = join(process.cwd(), 'artifacts/localization-browser'); mkdirSync(directory, { recursive: true });
const tenant = config.TenantId, ui = 'http://localhost:4000', identity = 'http://localhost:5181', catalog = 'http://localhost:5185';
const stamp = Date.now(), name = 'Locale verification ' + stamp, slug = 'locale-verification-' + stamp;
const hindiName = 'स्थानीय मसाला परीक्षण', hindiSeo = 'हिन्दी में मसाला परीक्षण';
const checks = [], errors = []; let step = 'merchant editor', product = null, archived = false;
const browser = await chromium.launch({ executablePath, headless: true });
const page = await browser.newPage({ viewport: { width: 1365, height: 900 } }); page.setDefaultTimeout(45000);
page.on('pageerror', error => errors.push(error.name));
function assert(value, message) { if (!value) throw new Error(message); }
function record(check) { checks.push(check); console.log(JSON.stringify({ passed: check })); }
try {
  await page.goto(ui + '/workspace/' + tenant + '/catalog', { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: 'Secure sign in', exact: true }).click();
  await page.waitForURL(url => url.origin === identity && url.pathname === '/account/login');
  await page.locator('input[name=email]').fill(config.OwnerEmail); await page.locator('input[name=password]').fill(config.OwnerPassword);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  step = 'merchant catalog load';
  await page.locator('.product-table tbody tr').first().waitFor();
  await page.getByRole('button', { name: 'New product', exact: true }).click();
  step = 'translation draft';
  const editor = page.locator('section.editor');
  await editor.getByLabel('Name', { exact: true }).fill(name); await editor.getByLabel('URL slug', { exact: true }).fill(slug);
  const category = await editor.locator('select[name=category]').evaluate(select => Array.from(select.options).find(option => option.value && !option.disabled)?.value);
  assert(category, 'CategoryUnavailable');
  await editor.locator('select[name=category]').selectOption(category); await editor.getByLabel('SKU', { exact: true }).fill('LOCALE-' + stamp);
  await editor.getByRole('button', { name: 'Add translation', exact: true }).click();
  await editor.getByRole('textbox', { name: 'Translated name', exact: true }).fill(hindiName);
  await editor.getByRole('textbox', { name: 'Translated description', exact: true }).fill('अपनी भाषा में उत्पाद की जानकारी।');
  await editor.getByLabel('Translated SEO title', { exact: true }).fill(hindiSeo);
  const saving = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/catalog/products'));
  void saving.catch(() => {});
  await editor.getByRole('button', { name: 'Save draft', exact: true }).click();
  const saved = await saving; assert(saved.ok(), 'TranslatedDraftRejected'); product = await saved.json();
  assert(product.translations?.hi?.name === hindiName, 'TranslatedDraftMissing');
  step = 'publish translated product';
  await page.locator('[name=catalog-search]').fill(slug);
  await page.getByRole('button', { name: 'Search products', exact: true }).click();
  const publication = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/products/' + product.id + '/publish'));
  void publication.catch(() => {});
  await page.locator('.product-table tbody tr').filter({ hasText: name }).getByRole('button', { name: 'Publish', exact: true }).click();
  const published = await publication; assert(published.ok(), 'TranslatedPublicationRejected'); product = await published.json();
  record('merchant Angular editor saves and publishes a Hindi product translation through Catalog SQL');

  step = 'Hindi product';
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(ui + '/stores/legacy-demo/products/' + slug, { waitUntil: 'domcontentloaded' });
  await page.getByRole('heading', { name, exact: true }).waitFor();
  await page.getByRole('combobox', { name: 'Store language', exact: true }).selectOption('hi');
  await page.getByRole('heading', { name: hindiName, exact: true }).waitFor();
  await page.getByRole('button', { name: 'कार्ट में जोड़ें', exact: true }).waitFor();
  await page.waitForFunction(title => document.title.startsWith(title), hindiSeo);
  assert(await page.locator('main.detail').getAttribute('lang') === 'hi', 'AccessibleLanguageMissing');
  assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'HindiProductOverflow');
  await page.screenshot({ path: join(directory, 'product-hindi-mobile.png'), fullPage: true });
  record('Hindi selection updates published text, shopping controls, SEO title and accessible page language');

  step = 'persisted cart language';
  await page.goto(ui + '/stores/legacy-demo/cart', { waitUntil: 'domcontentloaded' });
  await page.getByRole('heading', { name: 'आपका खरीदारी कार्ट', exact: true }).waitFor();
  assert(await page.getByRole('combobox', { name: 'Store language', exact: true }).inputValue() === 'hi', 'LanguagePreferenceLost');
  assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'HindiCartOverflow');
  await page.screenshot({ path: join(directory, 'cart-hindi-mobile.png'), fullPage: true });
  record('language preference survives full navigation to the mobile cart');

  step = 'older publication fallback';
  const mapping = JSON.parse(readFileSync('artifacts/legacy-demo-catalog-mapping.json', 'utf8'));
  const pageOfProducts = await (await request(catalog, '/api/v1/public/tenants/' + tenant + '/catalog/products?page=1&pageSize=24')).json();
  const originalIds = new Set(Object.values(mapping.products || {}).map(id => id.toLowerCase()));
  const original = pageOfProducts.items.find(item => originalIds.has(item.id.toLowerCase()) && !item.translations?.hi);
  assert(original, 'BaseLanguageProductUnavailable');
  await page.goto(ui + '/stores/legacy-demo/products/' + original.slug, { waitUntil: 'domcontentloaded' });
  await page.getByRole('heading', { name: original.name, exact: true }).waitFor();
  assert(await page.getByRole('combobox', { name: 'Store language', exact: true }).inputValue() === 'hi', 'FallbackChangedPreference');
  record('older published products retain their base content when no Hindi translation exists');

  step = 'English selection';
  await page.goto(ui + '/stores/legacy-demo/products/' + slug, { waitUntil: 'domcontentloaded' });
  await page.getByRole('heading', { name: hindiName, exact: true }).waitFor();
  await page.getByRole('combobox', { name: 'Store language', exact: true }).selectOption('en');
  await page.getByRole('heading', { name, exact: true }).waitFor();
  await page.getByRole('button', { name: 'Add to cart', exact: true }).waitFor();
  record('switching back to English restores base product and shopping controls');
  assert(errors.length === 0, 'BrowserRuntimeErrors'); record('no uncaught browser runtime errors');
} catch (error) {
  process.exitCode = 1; try { await page.screenshot({ path: join(directory, 'failure.png'), fullPage: true }); } catch {}
  console.error(JSON.stringify({ step, failureType: error.name === 'Error' && /^[A-Za-z]+$/.test(error.message) ? error.message : error.name, privateDetailsOmitted: true }));
} finally {
  if (product) {
    try {
      const token = await signIn(identity, config.OwnerEmail, config.OwnerPassword, tenant);
      const path = '/api/v1/tenants/' + tenant + '/catalog/products/' + product.id;
      const current = await (await request(catalog, path, { token })).json();
      assert(current.slug === slug, 'CleanupProductMismatch');
      if (current.status !== 2) await request(catalog, path + '/archive', { method: 'POST', token, status: 204, body: { version: current.version } });
      archived = true;
    } catch { process.exitCode = 1; }
  }
  await browser.close();
  writeFileSync(join(directory, 'result.json'), JSON.stringify({ sampledAt: new Date().toISOString(), checks, runtimeErrorTypes: errors, labelledProductArchived: archived, success: !process.exitCode }, null, 2));
  console.log(JSON.stringify({ checks: checks.length, labelledProductArchived: archived, success: !process.exitCode }));
}
