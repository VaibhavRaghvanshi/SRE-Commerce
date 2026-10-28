export type HomeSectionType = 1 | 2 | 3;
// 1=CategoriesGrid, 2=BannerSlider, 3=ProductsGrid

export interface HomeBlock {
  type: HomeSectionType;
  title?: string | null;
  sortOrder: number;
  itemsToShow: number;
  cardsPerRow: number;
  sourceKey?: string | null;
  styleJson?: string | null;
  items: any[]; // cast per section
}

export interface HomeResponse {
  sections: HomeBlock[];
}

export interface HomeBanner {
  imageUrl: string;
  link: string;
}
