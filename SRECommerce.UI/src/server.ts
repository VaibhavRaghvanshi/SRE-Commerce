import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express from 'express';
import { join } from 'node:path';
import { Readable } from 'node:stream';

const browserDistFolder = join(import.meta.dirname, '../browser');

const app = express();
const angularApp = new AngularNodeAppEngine();
const apiProxyTarget = process.env['API_PROXY_TARGET'];
const apiProxyTimeoutMs = Number(process.env['API_PROXY_TIMEOUT_MS'] || 15000);

if (apiProxyTarget) {
  app.use(['/api', '/uploads'], async (req, res, next) => {
    const abortController = new AbortController();
    const timeoutId = setTimeout(() => abortController.abort(), apiProxyTimeoutMs);

    try {
      const targetUrl = new URL(req.originalUrl, apiProxyTarget);
      const headers = new Headers();

      for (const [key, value] of Object.entries(req.headers)) {
        if (value === undefined || key.toLowerCase() === 'host') continue;
        if (Array.isArray(value)) {
          value.forEach(v => headers.append(key, v));
        } else {
          headers.set(key, value);
        }
      }

      const upstream = await fetch(targetUrl, {
        method: req.method,
        headers,
        body: ['GET', 'HEAD'].includes(req.method) ? undefined : Readable.toWeb(req) as BodyInit,
        duplex: ['GET', 'HEAD'].includes(req.method) ? undefined : 'half',
        redirect: 'manual',
        signal: abortController.signal
      } as RequestInit);

      res.status(upstream.status);
      upstream.headers.forEach((value, key) => {
        if (!['content-encoding', 'transfer-encoding'].includes(key.toLowerCase())) {
          res.setHeader(key, value);
        }
      });

      if (!upstream.body) {
        res.end();
        return;
      }

      Readable.fromWeb(upstream.body as any).pipe(res);
    } catch (error) {
      next(error);
    } finally {
      clearTimeout(timeoutId);
    }
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
