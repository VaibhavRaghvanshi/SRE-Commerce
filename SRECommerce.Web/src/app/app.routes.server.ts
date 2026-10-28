import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  {
    path: '',
    renderMode: RenderMode.Client
  },
  {
    path: 'products',
    renderMode: RenderMode.Client
  },
  {
    path: 'c/:slug',
    renderMode: RenderMode.Client
  },
  {
    path: 'p/:slug',
    renderMode: RenderMode.Client
  },
  {
    path: 'cart',
    renderMode: RenderMode.Client
  },
  {
    path: 'account',
    renderMode: RenderMode.Client
  },
  {
    path: 'admin',
    renderMode: RenderMode.Client
  },
  {
    path: 'admin/**',
    renderMode: RenderMode.Client
  },
  {
    path: '**',
    renderMode: RenderMode.Client
  }
];
