// src/app/core/models/catalog.models.ts

export interface Category {
  id: number;
  name: string;
  slug: string;
  parentId?: number | null;
  imageUrl?: string | null;
}

export interface Product {
  id: number;
  name: string;
  slug: string;
  sku: string;
  price: number;
  discount?: number | null;
  stockQty: number;
  categoryId: number;
  imageUrl?: string | null;
}

export interface PagedResult<T> {
  items: T[];
  totalItems: number;
  page: number;
  pageSize: number;
}
