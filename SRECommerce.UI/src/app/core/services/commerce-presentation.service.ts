import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, of, timeout } from 'rxjs';
import { CommerceTenant } from '../models/commerce.models';

export interface Placement { visible: boolean; order: number; columns: number; }
export interface LayoutSection { id: string; kind: string; title: string; text: string | null; references: string[]; desktop: Placement; mobile: Placement; }
export interface LayoutBanner { id: string; title: string; imageUrl: string; alt: string; link: string; desktop: Placement; mobile: Placement; }
export interface LayoutLink { id: string; label: string; href: string; }
export interface Layout {
  schemaVersion: number; template: string;
  header: { showSearch: boolean; showCategories: boolean; showCart: boolean; sticky: boolean };
  footer: { copyright: string; showContact: boolean; contactText: string | null };
  menus: { position: string; links: LayoutLink[] }[]; banners: LayoutBanner[]; sections: LayoutSection[];
  product: { reviews: boolean; description: boolean; specifications: boolean; businessFields: string[] };
}
export interface Typography { font: string; fontSize: number; radius: number; spacing: string; headingScale: number; lineHeight: number; }
export interface ThemeDocument { schemaVersion: number; defaultMode: 'light' | 'dark' | 'system';
  light: { colors: Record<string, string> }; dark: { colors: Record<string, string> }; desktop: Typography; mobile: Typography; }
export interface PublicLayout { tenantId: string; slug: string; source: string; sequence: number; document: Layout; }
export interface PublicTheme { tenantId: string; sequence: number; source: string; variables: Record<string, string>; document: ThemeDocument; }
export const colorKeys = ['primary','secondary','accent','background','surface','text','mutedText','border','headerBackground','headerText','footerBackground','footerText','buttonBackground','buttonText','cardBackground'];

export function safeThemeVariables(variables: Record<string, string> | undefined): Record<string, string> {
  const colors = new Set(colorKeys.map(x => '--color-' + x.replace(/[A-Z]/g, c => '-' + c.toLowerCase())));
  const result: Record<string, string> = {};
  for (const [key, value] of Object.entries(variables || {})) {
    if (typeof value !== 'string') continue;
    if (colors.has(key) && /^#[a-f0-9]{6}$/i.test(value) ||
        ['--font-size', '--border-radius'].includes(key) && /^(?:[0-9]|[12][0-9]|3[0-2])px$/.test(value) ||
        ['--heading-scale', '--line-height', '--spacing-scale'].includes(key) && /^(?:[012](?:\.[0-9]{1,4})?)$/.test(value) ||
        key === '--font-family' && ['Inter, sans-serif','Arial, sans-serif','Georgia, serif','system-ui, sans-serif'].includes(value)) result[key] = value;
  }
  return result;
}

@Injectable({ providedIn: 'root' })
export class CommercePresentationService {
  private readonly http = inject(HttpClient);
  layout(store: CommerceTenant, page = 'home') {
    return store.modules.includes('storefront') ? this.http.get<PublicLayout>(`/commerce/storefront/public/tenants/${encodeURIComponent(store.tenantId)}/pages/${encodeURIComponent(page)}`,
      { params: { businessType: store.businessType } }).pipe(timeout(3000), catchError(() => of(null))) : of(null);
  }
  theme(store: CommerceTenant, mode: 'light' | 'dark', mobile: boolean) {
    return store.modules.includes('themes') ? this.http.get<PublicTheme>(`/commerce/theme/public/tenants/${encodeURIComponent(store.tenantId)}/theme`,
      { params: { businessType: store.businessType, mode, mobile } }).pipe(timeout(3000), catchError(() => of(null))) : of(null);
  }
}
