import { OrderContact } from './commerce-order.models';

export interface CustomerAddress extends OrderContact { id: string; label: string; version: string; }
export interface CustomerAddressDraft extends OrderContact { label: string; version: string | null; }
export interface SavedProduct { productId: string; kind: number; available: boolean; name: string | null; slug: string | null; updatedAt: string; version: string; }
export interface SavedProductPage { items: SavedProduct[]; page: number; hasMore: boolean; }
