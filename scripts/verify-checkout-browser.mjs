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
const directory = 'artifacts/review-checkout'; mkdirSync(directory, { recursive: true });
const checks = []; let browser, page, orderId = null, checkoutId = null, target = null, before = null, step = 'preflight';
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
  step = 'product and cart'; await page.goto('http://localhost:4000/stores/legacy-demo/products/' + target.product.slug, { waitUntil: 'domcontentloaded' });
  await page.getByRole('heading', { name: target.product.name, exact: true }).waitFor(); step = 'initial price quote'; await page.getByTestId('quote-total').waitFor();
  step = 'save guest cart';
  await page.getByRole('button', { name: 'Add to cart', exact: true }).click(); await page.getByText('Added to your saved cart.', { exact: true }).waitFor();
  await page.getByRole('link', { name: /^View cart/ }).click(); await page.getByTestId('cart-total').waitFor();
  record('guest cart uses a mapped product and live server pricing/availability');
  step = 'prepare'; await page.getByRole('button', { name: 'Check checkout readiness', exact: true }).click();
  await page.getByRole('link', { name: /^Continue to checkout/ }).click();
  await page.getByRole('heading', { name: "A few details, then you're all set.", exact: true }).waitFor();
  await page.getByLabel('Full name', { exact: true }).fill('Local automated review customer'); await page.getByLabel('Email', { exact: true }).fill('local-review-customer@example.test');
  await page.getByLabel('Street address', { exact: true }).fill('1 Local Verification Street'); await page.getByLabel('City', { exact: true }).fill('Pune');
  await page.getByLabel('Country', { exact: true }).selectOption('IN'); await page.getByLabel('Postal code', { exact: true }).fill('411001');
  step = 'delivery'; await page.getByRole('button', { name: 'Check delivery options', exact: true }).click();
  await page.locator('label.choice').filter({ hasText: 'Local demo delivery' }).locator('input[type="radio"]').check();
  await page.getByLabel('Payment method', { exact: true }).selectOption('COD'); record('preparation-bound delivery and allowed sandbox COD options are returned');
  step = 'checkout'; const response = page.waitForResponse(reply => reply.request().method() === 'POST' && reply.url().endsWith('/orders/checkout'));
  await page.getByTestId('place-order').click(); const reply = await response;
  assert(reply.status() === 202, 'CheckoutRequestRejected'); const receipt = await reply.json(); checkoutId = receipt.id; orderId = receipt.order?.id || null;
  await page.getByRole('heading', { name: 'Your order is confirmed', exact: true }).waitFor();
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
      for (let i = 0; i < 20; i++) { if ((await availability(target.variant.id)).available === before.available) { restored = true; break; } await new Promise(resolve => setTimeout(resolve, 500)); }
      assert(restored, 'OriginalStockNotRestored'); cleanup.push('original mapped product availability restored');
    } else { cleanup.push('checkout reference requires scoped recovery review'); process.exitCode = 1; }
  } catch { cleanup.push('labelled checkout cleanup requires review; original records were not deleted'); process.exitCode = 1; }
  if (browser) await browser.close(); writeFileSync(join(directory, 'result.json'), JSON.stringify({ checks, checkoutId, orderId, cleanup, success: !process.exitCode }, null, 2));
  console.log(JSON.stringify({ checks: checks.length, cleanup, success: !process.exitCode }));
}
