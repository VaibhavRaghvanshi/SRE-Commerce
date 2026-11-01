export interface OrderContact {
  fullName: string; email: string; phone: string | null; addressLine1: string;
  addressLine2: string | null; city: string; countryCode: string; postalCode: string;
}
export interface CommerceOrderLine {
  productId: string; variantId: string; sku: string; name: string; quantity: number;
  fulfilled: number; delivered: number; returnRequested: number; returned: number;
  unitPrice: number; subtotal: number; discount: number; net: number; tax: number;
  total: number; shipmentRequested: number; refundAllocated: number;
}
export interface CommerceOrder {
  id: string; number: string; status: number; paymentState: number; paymentMethod: string;
  currency: string; decimalPlaces: number; subtotal: number; discount: number; net: number;
  tax: number; itemsTotal: number; shippingTotal: number; total: number; refunded: number;
  contact: OrderContact; items: CommerceOrderLine[]; invoiceReference: string | null;
  historical: boolean; createdAt: string; deliveredAt: string | null; version: string;
}
export interface CommerceOrderPage { items: CommerceOrder[]; total: number; page: number; pageSize: number; }
export interface CommerceOrderHistory { previous: number | null; current: number; reason: string; createdAt: string; }
export interface CommerceOrderPolicy {
  allowGuest: boolean; allowCOD: boolean; allowExchanges: boolean;
  returnWindowDays: number; maximumTotal: number; version: string;
}
export const ORDER_STATUSES = ['Awaiting payment', 'Confirmed', 'Processing', 'Partially fulfilled',
  'Fulfilled', 'Delivered', 'Completed', 'Cancelled', 'Failed', 'Needs review', 'Cancellation pending'];
export const PAYMENT_STATES = ['Pending', 'Authorized', 'Captured', 'Cash on delivery',
  'Partially refunded', 'Refunded', 'Failed'];
