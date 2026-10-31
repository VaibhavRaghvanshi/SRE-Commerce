import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express from 'express';
import { join } from 'node:path';
import { request as httpRequest } from 'node:http';
import { request as httpsRequest } from 'node:https';
import { pipeline } from 'node:stream';

const browserDistFolder = join(import.meta.dirname, '../browser');

const app = express();
const angularApp = new AngularNodeAppEngine();
const apiProxyTarget = process.env['API_PROXY_TARGET'];
const commerceProxyTarget = process.env['COMMERCE_PROXY_TARGET'];
const apiProxyTimeoutMs = Number(process.env['API_PROXY_TIMEOUT_MS'] || 15000);

if (apiProxyTarget || commerceProxyTarget) {
  app.use(['/api', '/uploads', '/commerce'], (req, res, next) => {
    const commerce = req.originalUrl === '/commerce' || req.originalUrl.startsWith('/commerce/');
    // Browser callback routes belong to Angular, not the resource gateway.
    if (commerce && !/^\/commerce\/(identity|tenant|theme|storefront|catalog|inventory|pricing|cart)(\/|\?|$)/.test(req.originalUrl)) { next(); return; }
    const proxyTarget = commerce ? commerceProxyTarget : apiProxyTarget;
    if (!proxyTarget) { next(); return; }
    const targetUrl = new URL(req.originalUrl, proxyTarget);
    const hopHeaders = new Set(['host', 'connection', 'keep-alive', 'proxy-authenticate', 'proxy-authorization', 'te', 'trailer', 'transfer-encoding', 'upgrade']);
    for (const header of (req.headers.connection || '').split(',')) hopHeaders.add(header.trim().toLowerCase());
    const headers = Object.fromEntries(Object.entries(req.headers).filter(([key]) => !hopHeaders.has(key)));
    const send = targetUrl.protocol === 'https:' ? httpsRequest : httpRequest;
    const upstream = send(targetUrl, { method: req.method, headers }, response => {
      res.status(response.statusCode || 502);
      const excluded = new Set([...hopHeaders, ...(response.headers.connection || '').split(',').map(h => h.trim().toLowerCase())]);
      for (const [key, value] of Object.entries(response.headers)) if (value !== undefined && !excluded.has(key)) res.setHeader(key, value);
      pipeline(response, res, () => clearTimeout(deadline));
    });
    const deadline = setTimeout(() => upstream.destroy(new Error('Upstream request timed out')), apiProxyTimeoutMs);
    upstream.on('error', () => { clearTimeout(deadline); if (!res.headersSent) res.status(502).json({ title: 'The API is temporarily unavailable.' }); else res.destroy(); });
    res.on('close', () => { clearTimeout(deadline); upstream.destroy(); });
    req.on('aborted', () => upstream.destroy());
    req.pipe(upstream);
  });
}

/**
 * Serve static files from /browser
 */
app.use(
  express.static(browserDistFolder, {
    maxAge: '1y',
    index: false,
    redirect: false,
  }),
);

/**
 * Handle all other requests by rendering the Angular application.
 */
app.use((req, res, next) => {
  angularApp
    .handle(req)
    .then((response) =>
      response ? writeResponseToNodeResponse(response, res) : next(),
    )
    .catch(next);
});

/**
 * Start the server if this module is the main entry point, or it is ran via PM2.
 * The server listens on the port defined by the `PORT` environment variable, or defaults to 4000.
 */
if (isMainModule(import.meta.url) || process.env['pm_id']) {
  const port = process.env['PORT'] || 4000;
  app.listen(port, (error) => {
    if (error) {
      throw error;
    }

    console.log(`Node Express server listening on http://localhost:${port}`);
  });
}

/**
 * Request handler used by the Angular CLI (for dev-server and during build) or Firebase Cloud Functions.
 */
export const reqHandler = createNodeRequestHandler(app);
