import { mkdirSync, writeFileSync } from 'node:fs';

// Warm and verify SQL-backed readiness serially on a small development laptop.
// No health bypass, credential provisioning, settings change or database writes.
const ports = { Identity:5181,Tenant:5182,Theme:5183,Storefront:5184,Catalog:5185,Inventory:5186,Pricing:5187,Cart:5188,Order:5189,Payment:5190,Shipping:5191,Notification:5192,Discovery:5194,ApiGateway:5180 };
const services = [];
for (const [service, port] of Object.entries(ports)) {
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
