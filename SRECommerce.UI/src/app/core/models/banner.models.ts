export interface Banner {
  id: number;
  title: string;
  imageUrl: string;
  linkType: number;
  linkValue?: string | null;
  isEnabled: boolean;
  sortOrder: number;
}

export interface UpsertBannerRequest {
  title: string;
  imageUrl: string;
  linkType: number;
  linkValue?: string | null;
  isEnabled: boolean;
  sortOrder: number;
}
