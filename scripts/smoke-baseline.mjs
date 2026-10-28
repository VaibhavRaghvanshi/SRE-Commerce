import assert from 'node:assert/strict';
import { randomBytes } from 'node:crypto';
import { writeFileSync } from 'node:fs';

// Run only against the separately restored smoke database described in the runbook.
const api = process.env.SRE_SMOKE_API_URL;
const web = process.env.SRE_SMOKE_WEB_URL;
if (!api || !web) throw new Error('Explicit SRE_SMOKE_API_URL and SRE_SMOKE_WEB_URL are required. Use an isolated restored database.');
const results = [];
async function request(path, { status = 200, token, method = 'GET', body, base = api } = {}) {
  const response = await fetch(base + path, {
    method,
    headers: { ...(body ? { 'Content-Type': 'application/json' } : {}), ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body ? JSON.stringify(body) : undefined,
    signal: AbortSignal.timeout(15000)
  });
  assert.equal(response.status, status, `${method} ${path} returned ${response.status}, expected ${status}`);
  results.push({ method, path, status: response.status, via: base === web ? 'production-web-proxy' : 'kestrel' });
  return response;
}

await request('/health/live');
await request('/health/ready');
const catalog = await (await request('/api/products?pageSize=12', { base: web })).json();
assert.equal(catalog.totalItems, 1000);
const home = await (await request('/api/home', { base: web })).json();
assert.ok(home.sections.length > 0);
assert.ok((await (await request('/', { base: web })).text()).includes('<app-root'));

const email = `smoke-${randomBytes(8).toString('hex')}@example.test`;
const password = randomBytes(24).toString('base64');
await request('/api/auth/register', { method: 'POST', body: { fullName: 'Isolated smoke customer', email, password } });
const auth = await (await request('/api/auth/login', { method: 'POST', body: { email, password }, base: web })).json();
assert.ok(Date.parse(auth.expiresAt) > Date.now() + 359 * 60000);
const token = auth.token;
const me = await (await request('/api/auth/me', { token, base: web })).json();
assert.equal(me.email, email);
await request('/api/admin/users', { token, status: 403 });
await request('/api/orders/mine', { status: 401 });
const product = catalog.items.find(p => p.stockQty > 0);
assert.ok(product);
const order = await (await request('/api/orders', {
  method: 'POST', status: 201, token, base: web,
  body: { customerName: 'Isolated smoke customer', customerEmail: email, phone: '0000000000', shippingAddress: 'Disposable smoke environment', items: [{ productId: product.id, quantity: 1 }] }
})).json();
assert.equal(order.total, Math.max(0, product.price - (product.discount || 0)) + 99);
const history = await (await request('/api/orders/mine', { token, base: web })).json();
assert.ok(history.some(item => item.orderNumber === order.orderNumber));
const updated = await (await request(`/api/products/${product.slug}`, { base: web })).json();
assert.equal(updated.stockQty, product.stockQty - 1);
const output = { completedUtc: new Date().toISOString(), checks: results.length, catalogProducts: catalog.totalItems, homeSections: home.sections.length, results };
if (process.env.SRE_SMOKE_REPORT) writeFileSync(process.env.SRE_SMOKE_REPORT, JSON.stringify(output, null, 2));
console.log(JSON.stringify({ checksPassed: results.length, catalogProducts: catalog.totalItems, homeSections: home.sections.length, tokensAndPasswordsLogged: false }));
