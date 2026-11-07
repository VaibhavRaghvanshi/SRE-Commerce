import { existsSync, readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { createRequire } from 'node:module';
import { request, signIn } from './lib/local-commerce.mjs';

if (!process.argv.includes('--apply')) { console.log(JSON.stringify({ applyRequired: true, scope: 'one labelled local COD order, then audited cancellation and stock reconciliation; no real money' })); process.exit(0); }
const require = createRequire(new URL('../SRECommerce.UI/package.json', import.meta.url)); const { chromium } = require('playwright-core');
const chrome = ['C:/Program Files/Google/Chrome/Application/chrome.exe', 'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe'].find(existsSync);
const owner = JSON.parse(readFileSync(join(process.env.APPDATA, 'Microsoft/UserSecrets/SRECommerce-LegacyDemo-Development/secrets.json'), 'utf8'));
const mapping = JSON.parse(readFileSync('artifacts/legacy-demo-catalog-mapping.json', 'utf8')), originals = new Set(Object.values(mapping.products));
if (mapping.tenantId !== owner.TenantId) throw new Error('DedicatedDemoTenantMismatch');
const tenant = owner.TenantId, publicPath = '/api/v1/public/tenants/' + tenant;
const verifyGst = process.argv.includes('--gst-worksheet');
const directory = verifyGst ? 'artifacts/review-gst-worksheet' : 'artifacts/review-checkout'; mkdirSync(directory, { recursive: true });
const checks = [], failedRequests = []; let browser, page, orderId = null, checkoutId = null, rejection = null, target = null, before = null, step = 'preflight';
function assert(value, message) { if (!value) throw new Error(message); }
function record(check) { checks.push(check); console.log(JSON.stringify({ passed: check })); }
async function availability(variant) { return (await (await request('http://localhost:5186', publicPath + '/inventory/availability?variants=' + variant)).json())[0]; }
try {
  step = 'public tenant modules';
  const registry = await (await request('http://localhost:5182', '/api/v1/public/tenants/legacy-demo')).json();
  assert(registry.tenantId === tenant && ['cart','orders','payments','shipping'].every(module => registry.modules.includes(module)), 'CheckoutModulesUnavailable');
  step = 'public catalog';
  const products = (await (await request('http://localhost:5185', publicPath + '/catalog/products?pageSize=24')).json()).items.filter(product => originals.has(product.id));
  const variants = products.flatMap(product => product.variants.map(variant => ({ product, variant })));
  step = 'available inventory';
  const stock = await (await request('http://localhost:5186', publicPath + '/inventory/availability?' + variants.map(row => 'variants=' + row.variant.id).join('&'))).json();
  target = variants.find(row => stock.some(balance => balance.variantId === row.variant.id && balance.tracked && balance.available >= 2)); assert(target, 'NoAvailableMappedProduct');
  before = stock.find(row => row.variantId === target.variant.id);
  step = 'browser startup';
  browser = await chromium.launch({ executablePath: chrome, headless: true }); page = await browser.newPage({ viewport: { width: 1365, height: 900 } }); page.setDefaultTimeout(45000);
  page.on('response', reply => { const path = new URL(reply.url()).pathname; if (path.startsWith('/commerce/') && !path.startsWith('/commerce/identity') && reply.status() >= 400) failedRequests.push({ path, status: reply.status() }); });
  step = 'product and cart'; await page.goto('http://localhost:4000/stores/legacy-demo/products/' + target.product.slug, { waitUntil: 'domcontentloaded' });
  await page.getByRole('heading', { name: target.product.name, exact: true }).waitFor(); step = 'initial price quote';
  const firstPrice = await Promise.race([
    page.getByTestId('quote-total').waitFor().then(() => true),
    page.getByText('Price or coupon is unavailable for this selection. Please check the quantity or try again.', { exact: true }).waitFor().then(() => false),
  ]);
  if (!firstPrice) {
    step = 'explicit preview refresh';
    await page.getByRole('button', { name: 'Apply / refresh price', exact: true }).click();
    await page.getByTestId('quote-total').waitFor();
    record('a transient initial price failure recovers through the visible refresh control; no cart/order write was replayed');
  }
  step = 'save guest cart';
  await page.getByRole('button', { name: 'Add to cart', exact: true }).click(); await page.getByText('Added to your saved cart.', { exact: true }).waitFor();
  await page.getByRole('link', { name: /^View cart/ }).click(); await page.getByTestId('cart-total').waitFor();
  record('guest cart uses a mapped product and live server pricing/availability');
  step = 'prepare'; await page.getByRole('button', { name: 'Check checkout readiness', exact: true }).click();
  await page.getByRole('link', { name: /^Continue to checkout/ }).click();
  await page.getByRole('heading', { name: "A few details, then you're all set.", exact: true }).waitFor();
  await page.getByLabel('Full name', { exact: true }).fill('Local automated review customer'); await page.getByLabel('Email', { exact: true }).fill('local-review-customer@example.test');
  await page.getByLabel('Street address', { exact: true }).fill('1 Local Verification Street'); await page.getByLabel('City', { exact: true }).fill('Pune');
  step = 'delivery contact';
  await page.locator('select[formcontrolname="countryCode"]').selectOption('IN'); await page.getByLabel('Postal code', { exact: true }).fill('411001');
  step = 'delivery'; await page.getByRole('button', { name: 'Check delivery options', exact: true }).click();
  await page.locator('label.choice').filter({ hasText: 'Local demo delivery' }).locator('input[type="radio"]').check();
  await page.locator('select[formcontrolname="paymentMethod"]').selectOption('COD'); record('preparation-bound delivery and allowed sandbox COD options are returned');
  step = 'checkout'; const response = page.waitForResponse(reply => reply.request().method() === 'POST' && reply.url().endsWith('/orders/checkout'));
  await page.getByTestId('place-order').click(); const reply = await response;
  if (reply.status() !== 202) { const problem = await reply.json().catch(() => ({})); rejection = { status: reply.status(), title: /^[A-Za-z][A-Za-z ;:.-]{1,100}$/.test(problem.title || '') ? problem.title : 'DetailsOmitted' }; }
  assert(reply.status() === 202, 'CheckoutRequestRejected'); const receipt = await reply.json(); checkoutId = receipt.id; orderId = receipt.order?.id || null;
  // Checkout is an asynchronous durable saga, not an immediate HTTP-200 order.
  await page.getByRole('heading', { name: 'Your order is confirmed', exact: true }).waitFor({ timeout: 180000 });
  const link = page.getByRole('link', { name: 'View order & tracking', exact: true });
  const href = await link.getAttribute('href'); orderId = /\/orders\/([a-f0-9-]{36})$/i.exec(href || '')?.[1] || orderId; assert(orderId, 'ConfirmedOrderMissing');
  record('real browser checkout confirms through Cart, Pricing, Inventory, Shipping, Payment and Order');
  await page.screenshot({ path: join(directory, 'confirmed-checkout.png'), fullPage: true });
  const token = await signIn('http://localhost:5181', owner.OwnerEmail, owner.OwnerPassword, tenant);
  const order = await (await request('http://localhost:5189', '/api/v1/tenants/' + tenant + '/order-management/orders/' + orderId, { token })).json();
  assert(!order.historical && order.paymentMethod === 'COD' && order.shippingTotal === 99 && order.items.length === 1 && order.items[0].variantId === target.variant.id && order.items[0].quantity === 1, 'OrderSnapshotMismatch');
  const after = await availability(target.variant.id); assert(after.available === before.available - 1, 'StockWasNotCommittedExactlyOnce'); record('server order snapshot and exactly-one-unit stock commit reconcile');
  await link.click(); await page.getByRole('heading', { name: 'Order details', exact: true }).waitFor();
  await page.setViewportSize({ width: 390, height: 844 }); assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'MobileOrderOverflow');
  await page.screenshot({ path: join(directory, 'order-mobile.png'), fullPage: true }); record('guest order history and tracking remain accessible on mobile');
  if (verifyGst) {
    step = 'merchant GST worksheet';
    page = await browser.newPage({ viewport: { width: 1365, height: 900 }, acceptDownloads: true });
    page.setDefaultTimeout(45000);
    await page.goto('http://localhost:4000/workspace/' + tenant + '/orders', { waitUntil: 'domcontentloaded' });
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    await page.waitForURL(url => url.origin === 'http://localhost:5181' && url.pathname === '/account/login');
    await page.locator('input[name=email]').fill(owner.OwnerEmail);
    await page.locator('input[name=password]').fill(owner.OwnerPassword);
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    await page.locator('tr').filter({ hasText: order.number }).getByRole('button', { name: 'View order', exact: true }).click();
    const worksheet = page.locator('app-gst-worksheet');
    await worksheet.getByText('GST preparation worksheet', { exact: true }).click();
    const fields = { 'supplier-name': 'Synthetic worksheet supplier', 'supplier-address': 'Local worksheet verification address',
      'supplier-gstin': '07ZZZZZ0000Z1Z0', 'place-name': 'Maharashtra', 'place-code': '27' };
    if (order.shippingTotal > 0) fields['shipping-sac'] = '996511';
    for (const [name, value] of Object.entries(fields)) await worksheet.locator(`[name="${name}"]`).fill(value);
    for (let index = 0; index < order.items.length; index++) {
      const item = worksheet.locator('.item').nth(index);
      await item.getByLabel('HSN', { exact: true }).fill('0904');
      await item.getByLabel('Unit code', { exact: true }).fill('PCS');
    }
    step = 'GST worksheet preview and download';
    const response = page.waitForResponse(reply => reply.request().method() === 'POST' && reply.url().endsWith('/gst-worksheet'));
    void response.catch(() => {});
    await worksheet.getByRole('button', { name: 'Preview worksheet', exact: true }).click();
    const preview = await response;
    assert(preview.ok(), 'WorksheetPreviewRejected');
    const result = await preview.json();
    assert(result.documentType === 'GST_WORKSHEET_DRAFT' && result.orderNumber === order.number && result.total === order.total
      && result.lines.length === order.items.length, 'WorksheetSnapshotMismatch');
    const download = page.waitForEvent('download');
    void download.catch(() => {});
    await worksheet.getByRole('button', { name: 'Download draft JSON', exact: true }).click();
    const file = await (await download).path();
    const exported = JSON.parse(readFileSync(file, 'utf8'));
    assert(exported.documentType === 'GST_WORKSHEET_DRAFT' && exported.total === order.total, 'WorksheetDownloadMismatch');
    await page.screenshot({ path: join(directory, 'worksheet-preview.png'), fullPage: true });
    record('merchant worksheet previews and downloads the recorded order amounts using synthetic GST fields');
  }
} catch (error) {
  process.exitCode = 1; if (page) try { await page.screenshot({ path: join(directory, 'failure.png'), fullPage: true }); } catch {}
  console.error(JSON.stringify({ step, failureType: error.name === 'Error' && /^[A-Za-z]+$/.test(error.message) ? error.message : error.name, privateDetailsOmitted: true }));
} finally {
  const cleanup = [];
  if (checkoutId) try {
    const token = await signIn('http://localhost:5181', owner.OwnerEmail, owner.OwnerPassword, tenant);
    const base = '/api/v1/tenants/' + tenant + '/order-management';
    if (!orderId) {
      const orders = await (await request('http://localhost:5189', base + '/orders', { token })).json();
      const own = orders.items.filter(order => !order.historical && order.contact?.email === 'local-review-customer@example.test');
      if (own.length === 1) orderId = own[0].id;
    }
    if (orderId) {
      const order = await (await request('http://localhost:5189', base + '/orders/' + orderId, { token })).json();
      assert(!order.historical && order.contact.email === 'local-review-customer@example.test', 'CleanupOwnershipMismatch');
      await request('http://localhost:5189', base + '/orders/' + orderId + '/cancel', { token, method: 'POST', body: { operationId: crypto.randomUUID(), version: order.version, reason: 'Cancel the labelled local browser verification order; no real customer purchase.' } });
      cleanup.push('only labelled verification order cancelled through audited business workflow');
      let restored = false;
      for (let i = 0; i < 120; i++) { if ((await availability(target.variant.id)).available === before.available) { restored = true; break; } await new Promise(resolve => setTimeout(resolve, 500)); }
      assert(restored, 'OriginalStockNotRestored'); cleanup.push('original mapped product availability restored');
    } else { cleanup.push('checkout reference requires scoped recovery review'); process.exitCode = 1; }
  } catch { cleanup.push('labelled checkout cleanup requires review; original records were not deleted'); process.exitCode = 1; }
  if (browser) await browser.close(); writeFileSync(join(directory, 'result.json'), JSON.stringify({ checks, failedRequests, rejection, checkoutId, orderId, cleanup, success: !process.exitCode }, null, 2));
  console.log(JSON.stringify({ checks: checks.length, cleanup, success: !process.exitCode }));
}
