export interface CommerceTenant {
  tenantId: string;
  slug: string;
  businessType: string;
  displayName: string;
  logoUrl: string | null;
  currency: string;
  modules: string[];
}
export interface CommerceLookup { id: string; name: string; slug: string; parentId?: string | null; }
export interface CommerceLookups { categories: CommerceLookup[]; brands: CommerceLookup[]; collections: CommerceLookup[]; }
export interface CommerceVariant { id: string; sku: string; options: Record<string, string>; barcode: string | null; active: boolean; }
export interface CommerceProduct {
  id: string;
  name: string;
  slug: string;
  categoryId: string;
  brandId: string | null;
  description: string;
  seoTitle: string;
  seoDescription: string;
  kind: number;
  status: number;
  visible: boolean;
  b2BOnly: boolean;
  attributes: Record<string, string | number | boolean>;
  specifications: Record<string, string>;
  variants: CommerceVariant[];
  media: { id: string; url: string; alt: string; order: number }[];
  bundleItems: { variantId: string; quantity: number }[];
  publishedRevision: number;
  version: string | null;
}
export interface CommercePage<T> { items: T[]; total: number; page: number; pageSize: number; }
