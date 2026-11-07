import { mkdirSync, writeFileSync } from 'node:fs';

// Warm and verify SQL-backed readiness serially on a small development laptop.
// No health bypass, credential provisioning, settings change or database writes.
const ports = { Identity:5181,Tenant:5182,Theme:5183,Storefront:5184,Catalog:5185,Inventory:5186,Pricing:5187,Cart:5188,Order:5189,Payment:5190,Shipping:5191,Notification:5192,Manufacturing:5193,Discovery:5194,ApiGateway:5180 };
const args = process.argv.slice(2);
if (args.length && (args.length !== 2 || args[0] !== '--services')) throw new Error('Use --services with a comma-separated list of known local services.');
const selected = args.length ? [...new Set(args[1].split(','))] : Object.keys(ports).filter(name => name !== 'Manufacturing');
if (!selected.length || selected.some(name => !Object.hasOwn(ports, name))) throw new Error('Unknown or empty local service group.');
const services = [];
for (const service of selected) {
  const port = ports[service];
  const started = Date.now(); let ready = false, status = null;
  try {
    const response = await fetch('http://localhost:' + port + '/health/ready', { signal: AbortSignal.timeout(15000) });
    status = response.status; const body = await response.json(); ready = response.ok && body.status === 'ready';
  } catch { /* Record unavailability without logging private diagnostics. */ }
  services.push({ service, port, ready, status, elapsedMs: Date.now() - started });
  console.log(JSON.stringify(services.at(-1)));
}
const success = services.every(row => row.ready);
mkdirSync('artifacts/review-website', { recursive: true });
writeFileSync('artifacts/review-website/readiness.json', JSON.stringify({ sampledAt: new Date().toISOString(), services, success }, null, 2));
if (!success) process.exitCode = 1;
