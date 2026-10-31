// src/app/app.routes.ts
import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';
import { userManagementGuard } from './core/guards/user-management.guard';

export const routes: Routes = [
  { path: 'workspace/:tenant/catalog', loadComponent: () => import('./features/commerce/workspace/catalog-workspace.component').then(m => m.CatalogWorkspaceComponent) },
  { path: 'commerce/callback', loadComponent: () => import('./features/commerce/auth/callback.component').then(m => m.CommerceCallbackComponent) },
  { path: 'stores/:slug', loadComponent: () => import('./features/commerce/catalog/catalog.component').then(m => m.CommerceCatalogComponent) },
  { path: 'stores/:slug/products/:product', loadComponent: () => import('./features/commerce/product/product.component').then(m => m.CommerceProductComponent) },
  // PUBLIC PAGES
  {
    path: '',
    loadComponent: () =>
      import('./features/public/home/home.component').then(m => m.HomeComponent)
  },
  {
    path: 'c/:slug',
    loadComponent: () =>
      import('./features/public/category/category.component').then(m => m.CategoryComponent)
  },
  {
    path: 'products',
    loadComponent: () =>
      import('./features/public/category/category.component').then(m => m.CategoryComponent)
  },
  {
    path: 'p/:slug',
    loadComponent: () =>
      import('./features/public/product-detail/product-detail.component').then(m => m.ProductDetailComponent)
  },
  {
    path: 'cart',
    loadComponent: () =>
      import('./features/public/cart/cart.component').then(m => m.CartComponent)
  },

  // AUTH
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'register',
    loadComponent: () =>
      import('./features/auth/register/register.component').then(m => m.RegisterComponent)
  },

  // CUSTOMER ACCOUNT (needs login)
  {
    path: 'account',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/account/account.component').then(m => m.AccountComponent)
  },

  // ADMIN SECTION
  {
    path: 'admin',
    canActivate: [adminGuard],
    canActivateChild: [adminGuard],
    loadComponent: () =>
      import('./features/admin/layout/admin-layout.component')
        .then(m => m.AdminLayoutComponent),
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/admin/dashboard/admin-dashboard.component')
            .then(m => m.AdminDashboardComponent)
      },
      {
        path: 'home-sections',
        loadComponent: () =>
          import('./features/admin/home-sections/admin-home-sections.component')
            .then(m => m.AdminHomeSectionsComponent)
      },
      {
        path: 'home-preview',
        loadComponent: () =>
          import('./features/admin/home-preview/admin-home-preview.component')
            .then(m => m.AdminHomePreviewComponent)
      },
      {
        path: 'banners',
        loadComponent: () =>
          import('./features/admin/banners/admin-banners.component')
            .then(m => m.AdminBannersComponent)
      },
      {
        path: 'categories',
        loadComponent: () =>
          import('./features/admin/categories/admin-categories.component')
            .then(m => m.AdminCategoriesComponent)
      },
      {
        path: 'products',
        loadComponent: () =>
          import('./features/admin/products/admin-products.component')
            .then(m => m.AdminProductsComponent)
      },
      {
        path: 'orders',
        loadComponent: () =>
          import('./features/admin/orders/admin-orders.component')
            .then(m => m.AdminOrdersComponent)
      },
      {
        path: 'users',
        canActivate: [userManagementGuard],
        loadComponent: () =>
          import('./features/admin/users/admin-users.component')
            .then(m => m.AdminUsersComponent)
      }
    ]
  },

  // CATCH ALL
  { path: '**', redirectTo: '' }];


