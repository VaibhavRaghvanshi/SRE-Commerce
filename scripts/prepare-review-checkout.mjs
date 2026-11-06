import { readFileSync, existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { request, signIn } from './lib/local-commerce.mjs';

if (!process.argv.includes('--apply')) { console.log(JSON.stringify({ applyRequired: true, scope: 'Legacy Demo order/payment/shipping settings only' })); process.exit(0); }
const owner = JSON.parse(readFileSync(join(process.env.APPDATA, 'Microsoft/UserSecrets/SRECommerce-LegacyDemo-Development/secrets.json'), 'utf8'));
const map = JSON.parse(readFileSync('artifacts/legacy-demo-catalog-mapping.json', 'utf8'));
if (map.tenantId !== owner.TenantId) throw new Error('DedicatedDemoTenantMismatch');
const token = await signIn('http://localhost:5181', owner.OwnerEmail, owner.OwnerPassword, owner.TenantId);
const tenantPath = '/api/v1/tenants/' + owner.TenantId;
const directory = 'artifacts/review-website'; mkdirSync(directory, { recursive: true });
const journalPath = join(directory, 'checkout-configuration.json');
const journal = existsSync(journalPath) ? JSON.parse(readFileSync(journalPath, 'utf8')) : { tenantId: owner.TenantId, zoneOperation: crypto.randomUUID(), methodOperation: crypto.randomUUID(), changes: [] };
if (journal.tenantId !== owner.TenantId) throw new Error('ReviewJournalTenantMismatch');
function save() { writeFileSync(journalPath, JSON.stringify(journal, null, 2)); } save();
const get = async (port, path) => (await request('http://localhost:' + port, path, { token })).json();
const send = async (port, path, body, method = 'POST') => (await request('http://localhost:' + port, path, { token, method, body })).json();
for (const module of ['payments', 'shipping']) {
  const rights = await get(5182, tenantPath + '/entitlements');
  if (!rights.modules.some(row => row.module === module && row.enabled)) {
    const tenant = await get(5182, tenantPath);
    await send(5182, tenantPath + '/modules/' + module, { version: tenant.version, enabled: true }, 'PUT'); journal.changes.push('enabled dedicated demo ' + module); save();
  }
}
const orderResponse = await fetch('http://localhost:5189' + tenantPath + '/order-management/policy', { headers: { Authorization: 'Bearer ' + token }, signal: AbortSignal.timeout(30000) });
let orderPolicy = await orderResponse.json();
if (orderResponse.status === 409 && orderPolicy.title === 'OrderingNotConfigured') orderPolicy = null;
else if (!orderResponse.ok) throw new Error('OrderPolicyReadRejected');
if (!orderPolicy) {
  orderPolicy = await send(5189, tenantPath + '/order-management/policy', { allowGuest: true, allowCOD: true, allowExchanges: true, returnWindowDays: 14, maximumTotal: 1000000, version: null }, 'PUT');
  journal.changes.push('created dedicated demo guest/COD order policy; 14-day returns and INR 1000000 cap'); save();
}
if (!orderPolicy.allowGuest || !orderPolicy.allowCOD) throw new Error('ExistingOrderPolicyPreservedNeedsReview');
const paymentResponse = await fetch('http://localhost:5190' + tenantPath + '/payments/configuration', { headers: { Authorization: 'Bearer ' + token }, signal: AbortSignal.timeout(30000) });
let payment = await paymentResponse.json();
if (paymentResponse.status === 404 && payment.title === 'PaymentsNotConfigured') payment = null;
else if (!paymentResponse.ok) throw new Error('PaymentConfigurationReadRejected');
if (payment && (payment.sandbox !== true || !/sandbox/i.test(payment.provider))) throw new Error('SandboxProviderRequired');
if (!payment) {
  payment = await send(5190, tenantPath + '/payments/configuration', { active: true, allowCard: false, allowUPI: true, allowNetBanking: false, allowCOD: true, outcome: 0, version: null }, 'PUT');
  journal.changes.push('created local sandbox UPI/COD configuration'); save();
}
if (!payment.active || !payment.allowCOD) throw new Error('ExistingPaymentPolicyPreservedNeedsReview');
let shipping = await get(5191, tenantPath + '/shipping/lookups');
let zone = shipping.zones.find(row => row.code === 'REVIEW-IN');
if (!zone) {
  zone = await send(5191, tenantPath + '/shipping/zones', { operationId: journal.zoneOperation, code: 'REVIEW-IN', name: 'Local demo · India', countries: ['IN'], postalPrefixes: [], active: true, version: null });
  journal.changes.push('created dedicated demo India region'); save();
}
if (!zone.active || !zone.countries.includes('IN')) throw new Error('ExistingReviewZonePreservedNeedsReview');
let method = shipping.methods.find(row => row.code === 'REVIEW-DELIVERY');
if (!method) {
  method = await send(5191, tenantPath + '/shipping/methods', { operationId: journal.methodOperation, zoneId: zone.id, code: 'REVIEW-DELIVERY', name: 'Local demo delivery', description: 'Development delivery option for the local review store.', kind: 0, currency: 'INR', flatFee: 99, perUnitFee: 0, taxRate: 0, freeAbove: null, minimumOrder: 0, maximumOrder: null, maximumUnits: 50000, pickupWarehouseId: null, active: true, version: null });
  journal.changes.push('created dedicated demo INR 99 delivery option'); save();
}
if (!method.active || method.zoneId !== zone.id || method.currency !== 'INR' || method.flatFee !== 99 || method.taxRate !== 0) throw new Error('ExistingDeliveryPolicyPreservedNeedsReview');
journal.verifiedAt = new Date().toISOString(); journal.sandboxOnly = true; journal.originalOrdersChanged = 0; save();
console.log(JSON.stringify({ demoTenantVerified: true, sandboxCOD: payment.allowCOD, sandboxUPI: payment.allowUPI, deliveryRegion: 'IN', developmentDeliveryFee: method.flatFee, originalOrdersChanged: 0 }));
