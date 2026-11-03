import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { request, signIn } from './lib/local-commerce.mjs';

const base = {
  identity: 'http://localhost:5181', tenant: 'http://localhost:5182',
  catalog: 'http://localhost:5185', discovery: 'http://localhost:5194'
};
const registry = await (await request(base.tenant, '/api/v1/public/tenants/legacy-demo')).json();
const tenant = registry.tenantId;
if (registry.slug !== 'legacy-demo' || !/^[0-9a-f-]{36}$/i.test(tenant || '') || !registry.modules.includes('catalog'))
  throw new Error('Expected the dedicated Legacy Demo storefront with an active catalog.');
const catalog = '/api/v1/public/tenants/' + tenant + '/catalog/products';
const first = await (await request(base.catalog, catalog + '?page=1&pageSize=50')).json();
if (!Number.isInteger(first.total) || first.total < 1 || first.total > 10000) throw new Error('Legacy Demo catalog count is outside the supported batch size.');
if (!process.argv.includes('--apply')) {
  console.log(JSON.stringify({ dryRun: true, store: registry.slug, products: first.total, pages: Math.ceil(first.total / 50) }));
  process.exit(0);
}
const owner = JSON.parse(readFileSync(join(process.env.APPDATA, 'Microsoft/UserSecrets/SRECommerce-LegacyDemo-Development/secrets.json'), 'utf8'));
if (owner.TenantId !== tenant) throw new Error('The local owner does not match the dedicated Legacy Demo tenant.');
await request(base.discovery, '/health/ready');
const token = await signIn(base.identity, owner.OwnerEmail, owner.OwnerPassword, tenant);
const tenantPath = '/api/v1/tenants/' + tenant;
if (!registry.modules.includes('discovery')) {
  const business = await (await request(base.tenant, tenantPath, { token })).json();
  await request(base.tenant, tenantPath + '/modules/discovery', { method: 'PUT', token, body: { version: business.version, enabled: true } });
}
const discovery = tenantPath + '/discovery/index/pages/';
let indexed = 0;
const pages = Math.ceil(first.total / 50);
for (let page = 1; page <= pages; page++) {
  const batch = await (await request(base.discovery, discovery + page, { method: 'POST', token })).json();
  if (batch.page !== page || batch.total !== first.total || batch.pageSize !== 50) throw new Error('The published catalog changed during indexing; retry the full batch.');
  indexed += batch.indexed;
}
if (indexed !== first.total) throw new Error('The index batch did not reconcile with the public catalog.');
const sample = (await (await request(base.catalog, catalog + '?pageSize=10&search=SR%20Phone%20X')).json()).items.find(x => x.name === 'SR Phone X');
if (!sample) throw new Error('Expected sample product is missing from the published catalog.');
const search = '/api/v1/public/tenants/' + tenant + '/discovery/search?' + new URLSearchParams({ q: 'SR Phone X', category: sample.categoryId });
for (let attempt = 0; attempt < 20; attempt++) {
  const results = await (await request(base.discovery, search)).json();
  if (results.items.some(x => x.productId === sample.id)) {
    console.log(JSON.stringify({ store: registry.slug, indexed, pages, verifiedSearch: true }));
    process.exit(0);
  }
  await new Promise(resolve => setTimeout(resolve, 500));
}
throw new Error('Full-text population did not show the known sample in the first result page.');
