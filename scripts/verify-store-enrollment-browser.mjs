import { existsSync, readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { createRequire } from 'node:module';
import { randomBytes } from 'node:crypto';
import { request, signIn } from './lib/local-commerce.mjs';

if (!process.argv.includes('--apply')) throw new Error('Use --apply to create one labelled local account and verify its membership lifecycle.');
const require = createRequire(new URL('../SRECommerce.UI/package.json', import.meta.url));
const { chromium } = require('playwright-core');
const executablePath = ['C:/Program Files/Google/Chrome/Application/chrome.exe', 'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe'].find(existsSync);
const owner = JSON.parse(readFileSync(join(process.env.APPDATA, 'Microsoft/UserSecrets/SRECommerce-LegacyDemo-Development/secrets.json'), 'utf8'));
const identity = 'http://localhost:5181', ui = 'http://localhost:4000', tenant = owner.TenantId, slug = 'legacy-demo';
const email = 'enrollment-' + Date.now() + '@example.test', password = 'Local-' + randomBytes(20).toString('hex') + 'Aa1!';
const directory = join(process.cwd(), 'artifacts/store-enrollment-browser'); mkdirSync(directory, { recursive: true });
const checks = [], errors = []; let step = 'registration', customerId = null, ownerToken = null, cleanup = false;
const responses = [];
let observedJoin = null; const observedAccess = [];
const browser = await chromium.launch({ executablePath, headless: true });
const page = await browser.newPage({ viewport: { width: 390, height: 844 } }); page.setDefaultTimeout(45000);
page.on('pageerror', error => errors.push(error.name));
page.on('response', response => {
  const path = new URL(response.url()).pathname;
  if (path.startsWith('/commerce/identity/') || path.startsWith('/connect/') || path === '/account/login')
    responses.push({ path, status: response.status() });
});
function assert(value, message) { if (!value) throw new Error(message); }
function record(check) { checks.push(check); console.log(JSON.stringify({ passed: check })); }
try {
  await page.route('**/commerce/identity/me/access', async route => {
    const response = await route.fetch();
    if (response.ok()) {
      const data = await response.json();
      observedAccess.push({ userId: data.userId, selectedTenantId: data.selectedTenantId, tenantRole: data.tenantRole });
      customerId = data.userId;
    }
    await route.fulfill({ response });
  });
  await page.route('**/commerce/identity/me/stores/' + tenant + '/membership', async route => {
    const response = await route.fetch();
    if (response.ok()) observedJoin = await response.json();
    await route.fulfill({ response });
  });
  await page.goto(ui + '/stores/' + slug + '/account', { waitUntil: 'domcontentloaded' });
  await page.getByRole('link', { name: 'Create an account', exact: true }).click();
  await page.waitForURL(url => url.pathname === '/commerce/account' && url.searchParams.get('store') === slug);
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Full name', { exact: true }).fill('Local enrollment verification');
  await page.getByLabel('New password', { exact: true }).fill(password);
  const registration = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/accounts/register'));
  await page.getByRole('button', { name: 'Continue', exact: true }).click();
  assert((await registration).status() === 202, 'RegistrationRejected');
  let code;
  const deadline = Date.now() + 45000;
  while (!code && Date.now() < deadline) {
    const messages = await (await request('http://localhost:8025', '/api/v1/messages')).json();
    const message = messages.messages.find(m => m.To?.some(to => to.Address === email));
    if (message) {
      const content = await (await request('http://localhost:8025', '/api/v1/message/' + message.ID)).json();
      const data = JSON.parse(content.Text.trim()); if (data.purpose === 'confirm-email') code = data.code;
    }
    if (!code) await new Promise(resolve => setTimeout(resolve, 500));
  }
  assert(code, 'LocalConfirmationMissing');
  step = 'email confirmation';
  await page.getByRole('combobox').first().selectOption('confirm-email');
  await page.getByLabel('Code from email', { exact: true }).fill(code); code = undefined;
  const confirmation = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/accounts/confirm-email'));
  await page.getByRole('button', { name: 'Continue', exact: true }).click();
  assert((await confirmation).status() === 204, 'ConfirmationRejected');
  record('fresh customer registers and confirms email through Angular and loopback Mailpit');

  step = 'store sign-in';
  await page.getByRole('button', { name: 'Sign in with SRECommerce Identity', exact: true }).click();
  step = 'identity login form';
  await page.waitForURL(url => url.origin === identity && url.pathname === '/account/login');
  assert(await page.locator('article').evaluate(node => getComputedStyle(node).borderRadius === '18px'), 'IdentityStylesBlocked');
  assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'IdentityMobileOverflow');
  await page.screenshot({ path: join(directory, 'sign-in-mobile.png'), fullPage: true });
  record('Identity sign-in styling works with the CSP on a mobile viewport');
  await page.locator('input[name=email]').fill(email); await page.locator('input[name=password]').fill(password);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.waitForURL(ui + '/stores/' + slug + '/account');
  await page.getByRole('button', { name: 'Add address', exact: true }).waitFor();
  assert(observedJoin?.role === 'Customer', 'CustomerJoinRejected');
  assert(observedAccess.some(access => access.selectedTenantId === tenant && access.tenantRole === 'Customer'), 'UnexpectedCustomerRole');
  assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'MobileOverflow');
  await page.screenshot({ path: join(directory, 'customer-account-mobile.png'), fullPage: true });
  record('two-stage PKCE returns a new Customer to the original mobile store account');

  step = 'membership replay';
  const globalToken = await signIn(identity, email, password);
  const retry = await (await request(identity, '/api/v1/me/stores/' + tenant + '/membership', { method: 'POST', token: globalToken, body: { role: 'Owner' } })).json();
  assert(retry.role === 'Customer' && !retry.joined, 'EnrollmentReplayChangedRole');
  record('duplicate enrollment is idempotent and cannot select an administrative role');

  step = 'owner administration';
  ownerToken = await signIn(identity, owner.OwnerEmail, owner.OwnerPassword, tenant);
  const membership = '/api/v1/tenants/' + tenant + '/memberships/' + customerId;
  await request(identity, membership, { method: 'PUT', token: ownerToken, status: 204, body: { userId: customerId, role: 'Manager' } });
  const managerToken = await signIn(identity, email, password, tenant);
  await request(identity, membership, { method: 'PUT', token: managerToken, status: 403, body: { userId: customerId, role: 'Manager' } });
  await request(identity, membership, { method: 'DELETE', token: ownerToken, status: 204 }); cleanup = true;
  const revoked = await fetch(identity + '/api/v1/me', { headers: { Authorization: 'Bearer ' + managerToken }, signal: AbortSignal.timeout(15000) });
  assert([401, 403].includes(revoked.status), 'ManagerSessionNotRevoked');
  await request(identity, '/api/v1/me/stores/' + tenant + '/membership', { method: 'POST', token: globalToken, body: {}, status: 403 });
  record('Owner manages the synthetic Manager, revokes its session, and blocked membership cannot self-reactivate');
  assert(errors.length === 0, 'BrowserRuntimeErrors'); record('no uncaught browser runtime errors');
} catch (error) {
  process.exitCode = 1;
  try { await page.screenshot({ path: join(directory, 'failure.png'), fullPage: true }); } catch {}
  console.error(JSON.stringify({ step, path: new URL(page.url()).pathname, failureType: error.name === 'Error' && /^[A-Za-z]+$/.test(error.message) ? error.message : error.name, privateDetailsOmitted: true }));
} finally {
  if (customerId && observedJoin && !cleanup) {
    try {
      ownerToken ||= await signIn(identity, owner.OwnerEmail, owner.OwnerPassword, tenant);
      await request(identity, '/api/v1/tenants/' + tenant + '/memberships/' + customerId, { method: 'DELETE', token: ownerToken, status: 204 }); cleanup = true;
    } catch { process.exitCode = 1; }
  }
  await browser.close();
  writeFileSync(join(directory, 'result.json'), JSON.stringify({ sampledAt: new Date().toISOString(), checks, responses, runtimeErrorTypes: errors, labelledMembershipDeactivated: cleanup, success: !process.exitCode }, null, 2));
  console.log(JSON.stringify({ checks: checks.length, labelledMembershipDeactivated: cleanup, success: !process.exitCode }));
}
