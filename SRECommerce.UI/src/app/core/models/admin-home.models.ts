export type AdminHomeSectionType = 'categories' | 'banners' | 'products';

export interface AdminHomeSection {
  id: number;
  type: number; // 1 CategoriesGrid, 2 BannerSlider, 3 ProductsGrid
  title?: string | null;
  isEnabled: boolean;
  sortOrder: number;
  itemsToShow: number;
  cardsPerRow: number;
  sourceKey?: string | null;
  styleJson?: string | null;
}

export interface UpsertHomeSectionRequest {
  type: number;
  title?: string | null;
  isEnabled: boolean;
  sortOrder: number;
  itemsToShow: number;
  cardsPerRow: number;
  sourceKey?: string | null;
  styleJson?: string | null;
}

export interface HomeSectionItem {
  id: number;
  entityType: number; // 1 Category, 2 Product, 3 Banner
  entityId: number;
  sortOrder: number;
  isEnabled: boolean;
  title?: string | null;
  imageUrl?: string | null;
}

export interface HomeSectionItemUpsert {
  entityType: number;
  entityId: number;
  sortOrder: number;
  isEnabled: boolean;
}

export interface UpsertHomeSectionItemsRequest {
  items: HomeSectionItemUpsert[];
}
