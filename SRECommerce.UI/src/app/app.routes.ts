// src/app/app.routes.ts
import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';
import { userManagementGuard } from './core/guards/user-management.guard';

export const routes: Routes = [
  { path: 'workspace/:tenant/reports', loadComponent: () => import('./features/commerce/reports/sales-dashboard.component').then(m => m.SalesDashboardComponent) },
  { path: 'stores/:slug/account', loadComponent: () => import('./features/commerce/customer-account/customer-account.component').then(m => m.CommerceCustomerAccountComponent) },
  { path: 'commerce/account', loadComponent: () => import('./features/commerce/account/commerce-account.component').then(m => m.CommerceAccountComponent) },
  { path: 'businesses', loadComponent: () => import('./features/commerce/tenant/business-workspace.component').then(m => m.BusinessWorkspaceComponent) },
  { path: 'platform/workspace', data: { platform: true }, loadComponent: () => import('./features/commerce/tenant/business-workspace.component').then(m => m.BusinessWorkspaceComponent) },
  { path: 'workspace/:tenant/settings', loadComponent: () => import('./features/commerce/tenant/business-workspace.component').then(m => m.BusinessWorkspaceComponent) },
  { path: 'workspace/:tenant/theme', data: { presentation: 'theme' }, loadComponent: () => import('./features/commerce/presentation/presentation-editor.component').then(m => m.PresentationEditorComponent) },
  { path: 'workspace/:tenant/storefront', data: { presentation: 'layout' }, loadComponent: () => import('./features/commerce/presentation/presentation-editor.component').then(m => m.PresentationEditorComponent) },
  { path: 'workspace/:tenant/manufacturing', loadComponent: () => import('./features/commerce/manufacturing/manufacturing.component').then(m => m.ManufacturingComponent) },
  { path: 'stores/:slug/search', loadComponent: () => import('./features/commerce/discovery/discovery.component').then(m => m.DiscoveryComponent) },
  { path: 'stores/:slug/pages/:page', loadComponent: () => import('./features/commerce/presentation/storefront-page.component').then(m => m.StorefrontPageComponent) },
  { path: 'workspace/:tenant/discovery', loadComponent: () => import('./features/commerce/discovery/discovery.component').then(m => m.DiscoveryComponent) },
  { path: 'stores/:slug/notifications', loadComponent: () => import('./features/commerce/notifications/notifications.component').then(m => m.NotificationsComponent) },
  { path: 'workspace/:tenant/notifications', loadComponent: () => import('./features/commerce/notifications/notifications.component').then(m => m.NotificationsComponent) },
  { path: 'stores/:slug/checkout', loadComponent: () => import('./features/commerce/checkout/checkout.component').then(m => m.CommerceCheckoutComponent) },
  { path: 'stores/:slug/orders/:order', loadComponent: () => import('./features/commerce/customer-orders/customer-orders.component').then(m => m.CommerceCustomerOrdersComponent) },
  { path: 'stores/:slug/orders', loadComponent: () => import('./features/commerce/customer-orders/customer-orders.component').then(m => m.CommerceCustomerOrdersComponent) },
  { path: 'workspace/:tenant/shipping', loadComponent: () => import('./features/commerce/shipping/shipping-workspace.component').then(m => m.ShippingWorkspaceComponent) },
  { path: 'workspace/:tenant/payments', loadComponent: () => import('./features/commerce/payments/payments-workspace.component').then(m => m.PaymentsWorkspaceComponent) },
  { path: 'workspace/:tenant/orders/:order/workflow', loadComponent: () => import('./features/commerce/orders/order-workflow.component').then(m => m.OrderWorkflowComponent) },
  { path: 'workspace/:tenant/orders', loadComponent: () => import('./features/commerce/orders/orders-workspace.component').then(m => m.OrdersWorkspaceComponent) },
  { path: 'workspace/:tenant/pricing', loadComponent: () => import('./features/commerce/pricing/pricing-workspace.component').then(m => m.PricingWorkspaceComponent) },
  { path: 'workspace/:tenant/inventory', loadComponent: () => import('./features/commerce/inventory/inventory-workspace.component').then(m => m.InventoryWorkspaceComponent) },
  { path: 'workspace/:tenant/catalog', loadComponent: () => import('./features/commerce/workspace/catalog-workspace.component').then(m => m.CatalogWorkspaceComponent) },
  { path: 'commerce/callback', loadComponent: () => import('./features/commerce/auth/callback.component').then(m => m.CommerceCallbackComponent) },
  { path: 'stores/:slug/cart', loadComponent: () => import('./features/commerce/cart/cart.component').then(m => m.CommerceCartComponent) },
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


