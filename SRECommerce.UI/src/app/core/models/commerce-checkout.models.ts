import { CommerceOrder, OrderContact } from './commerce-order.models';
export interface CommerceShippingOption { id: string; methodId: string; methodName: string; description: string; kind: number; currency: string; countryCode: string; postalCode: string; net: number; tax: number; total: number; expiresAt: string; preparationId: string; }
export interface CommercePaymentMethods { provider: string; sandbox: boolean; configured: boolean; methods: string[]; }
export interface CommerceCheckout { id: string; state: number; phase: number; issue: string | null; createdAt: string; order: CommerceOrder | null; }
export interface CommerceCheckoutRequest { id: string; preparationId: string; shippingQuoteId: string; paymentMethod: string; contact: OrderContact; }
export interface SavedOrderReference { id: string; guest: boolean; userId: string | null; createdAt: string; state: number; }
export interface CommerceTracking { shipment: { id: string; orderId: string; kind: number; provider: string; courierReference: string; status: number; items: { variantId: string; quantity: number }[]; dispatchedAt: string | null; deliveredAt: string | null }; events: { status: number; note: string; occurredAt: string }[]; }
export interface CommerceReturn { id: string; orderId: string; items: { variantId: string; quantity: number }[]; reason: string; status: number; refundAmount: number; warehouseId: string | null; resalable: boolean; exchangeId: string | null; version: string; }
export interface CommerceExchange { id: string; orderId: string; returnId: string; originalVariantId: string; replacementVariantId: string; quantity: number; status: number; replacementOrderId: string | null; version: string; }
