import { existsSync, readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { createRequire } from 'node:module';

const require = createRequire(new URL('../SRECommerce.UI/package.json', import.meta.url));
const { chromium } = require('playwright-core');
const chrome = ['C:/Program Files/Google/Chrome/Application/chrome.exe', 'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe'].find(existsSync);
const root = join(process.cwd(), 'artifacts/review-design'); mkdirSync(root, { recursive: true });
const owner = JSON.parse(readFileSync(join(process.env.APPDATA, 'Microsoft/UserSecrets/SRECommerce-LegacyDemo-Development/secrets.json'), 'utf8'));
const browser = await chromium.launch({ executablePath: chrome, headless: true });
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } }); page.setDefaultTimeout(30000);
const errors = [], checks = []; page.on('pageerror', error => errors.push(error.name)); let step = 'homepage';
function assert(value, message) { if (!value) throw new Error(message); }
function record(check) { checks.push(check); console.log(JSON.stringify({ passed: check })); }
async function layout() { assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'HorizontalOverflow'); }
try {
  await page.goto('http://localhost:4000/', { waitUntil: 'domcontentloaded' });
  await page.getByRole('heading', { name: /Discover good things/ }).waitFor(); await page.locator('.product-grid article').first().waitFor();
  await layout(); await page.screenshot({ path: join(root, 'home-desktop.png'), fullPage: true }); record('homepage uses live public Catalog data and has no desktop overflow');
  await page.setViewportSize({ width: 390, height: 844 }); await layout(); await page.screenshot({ path: join(root, 'home-mobile.png'), fullPage: true }); record('homepage adapts to a 390px mobile viewport');
  step = 'storefront'; await page.goto('http://localhost:4000/stores/legacy-demo', { waitUntil: 'domcontentloaded' }); await page.locator('.product-grid article').first().waitFor(); await layout();
  await page.screenshot({ path: join(root, 'store-mobile.png'), fullPage: true }); record('SQL-backed storefront renders on mobile');
  step = 'merchant sign-in'; await page.setViewportSize({ width: 1440, height: 1000 }); await page.goto('http://localhost:4000/workspace/' + owner.TenantId + '/catalog');
  await page.getByRole('button', { name: 'Secure sign in', exact: true }).click(); await page.locator('input[name="email"]').fill(owner.OwnerEmail); await page.locator('input[name="password"]').fill(owner.OwnerPassword);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click(); await page.getByRole('button', { name: 'New product', exact: true }).waitFor(); await page.locator('.product-table tbody tr').first().waitFor();
  await page.getByRole('navigation', { name: 'Workspace navigation', exact: true }).waitFor(); await layout(); await page.screenshot({ path: join(root, 'workspace-desktop.png'), fullPage: true }); record('merchant workspace and navigation work through real PKCE sign-in');
  step = 'mobile workspace'; await page.setViewportSize({ width: 390, height: 844 }); await layout(); await page.screenshot({ path: join(root, 'workspace-mobile.png'), fullPage: true }); record('workspace keeps its table inside the mobile scroll container');
  step = 'catalog controls'; await page.locator('summary').filter({ hasText: 'Catalog setup & bulk tools' }).click();
  await page.locator('app-catalog-fields').getByRole('heading', { name: 'Product fields', exact: true }).waitFor(); await page.locator('app-catalog-transfer').getByRole('heading', { name: 'Import & export', exact: true }).waitFor(); await layout();
  await page.screenshot({ path: join(root, 'catalog-tools-mobile.png'), fullPage: true }); record('field management and import/export controls are reachable on mobile');
  assert(errors.length === 0, 'BrowserRuntimeErrors'); record('no uncaught browser runtime errors');
} catch (error) {
  process.exitCode = 1; try { await page.screenshot({ path: join(root, 'failure.png'), fullPage: true }); } catch {}
  console.error(JSON.stringify({ step, failureType: error.name === 'Error' && /^[A-Za-z]+$/.test(error.message) ? error.message : error.name, privateDetailsOmitted: true }));
} finally {
  await browser.close(); writeFileSync(join(root, 'result.json'), JSON.stringify({ checks, runtimeErrorTypes: errors, success: !process.exitCode }, null, 2));
  console.log(JSON.stringify({ checks: checks.length, success: !process.exitCode }));
}
