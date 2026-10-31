import { Product } from './catalog.models';

export interface CartItem {
  product: Product;
  quantity: number;
}

export interface CreateOrderItemRequest {
  productId: number;
  quantity: number;
}

export interface CreateOrderRequest {
  customerName: string;
  customerEmail: string;
  phone: string;
  shippingAddress: string;
  items: CreateOrderItemRequest[];
}

export interface OrderItem {
  productId: number;
  productName: string;
  sku: string;
  imageUrl?: string | null;
  unitPrice: number;
  discount: number;
  quantity: number;
  lineTotal: number;
}

export interface Order {
  id: number;
  orderNumber: string;
  customerName: string;
  customerEmail: string;
  phone: string;
  shippingAddress: string;
  status: string;
  subtotal: number;
  discountTotal: number;
  shippingFee: number;
  total: number;
  createdAt: string;
  items: OrderItem[];
}

export interface AdminTopProduct {
  productName: string;
  quantitySold: number;
  revenue: number;
}

export interface AdminRecentOrder {
  orderNumber: string;
  customerName: string;
  total: number;
  status: string;
  createdAt: string;
}

export interface AdminSummary {
  totalRevenue: number;
  totalOrders: number;
  pendingOrders: number;
  deliveredOrders: number;
  totalProducts: number;
  totalCustomers: number;
  totalEmployees: number;
  totalStock: number;
  inventoryValue: number;
  topProducts: AdminTopProduct[];
  recentOrders: AdminRecentOrder[];
}
