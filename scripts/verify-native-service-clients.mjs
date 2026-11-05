import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

// The owner approved these nine local identities only. No credentials are generated,
// rotated, printed or written by this verifier. Tokens live only in process memory.
const services = ['Identity', 'Catalog', 'Inventory', 'Pricing', 'Cart', 'Order', 'Payment', 'Shipping', 'Notification'];
const identity = 'http://localhost:5181', checks = [];
for (const service of services) {
  try {
    const values = JSON.parse(readFileSync(join(process.env.APPDATA, 'Microsoft/UserSecrets/SRECommerce-' + service + '-Development/secrets.json'), 'utf8'));
    const body = new URLSearchParams({ grant_type: 'client_credentials', client_id: values['ServiceAuth:ClientId'], client_secret: values['ServiceAuth:ClientSecret'], scope: 'internal_tenant' });
    const response = await fetch(identity + '/connect/token', { method: 'POST', body, signal: AbortSignal.timeout(30000) });
    if (!response.ok) throw new Error('ServiceGrantRejected');
    const token = await response.json(); if (typeof token.access_token !== 'string') throw new Error('MissingServiceToken');
    const access = await fetch(identity + '/internal/v1/access', { headers: { Authorization: 'Bearer ' + token.access_token }, signal: AbortSignal.timeout(30000) });
    if (!access.ok) throw new Error('MachineContextRejected');
    const machine = await access.json();
    if (machine.service !== service || !machine.scopes?.includes('internal_tenant')) throw new Error('MachineIdentityMismatch');
    body.set('scope', 'identity_api');
    const denied = await fetch(identity + '/connect/token', { method: 'POST', body, signal: AbortSignal.timeout(30000) });
    if (![400, 403].includes(denied.status)) throw new Error('HumanScopeWasNotDenied');
    checks.push({ service, machineGrant: true, humanScopeDenied: true });
    console.log(JSON.stringify(checks.at(-1)));
  } catch (error) { checks.push({ service, verified: false, failureType: error.name === 'Error' && /^[A-Za-z]+$/.test(error.message) ? error.message : error.name }); process.exitCode = 1; }
}
const directory = join(process.cwd(), 'artifacts/review-website'); mkdirSync(directory, { recursive: true });
writeFileSync(join(directory, 'service-client-checks.json'), JSON.stringify({ sampledAt: new Date().toISOString(), checks, success: !process.exitCode }, null, 2));
console.log(JSON.stringify({ approvedClients: services.length, successful: checks.filter(row => row.machineGrant).length, success: !process.exitCode }));
