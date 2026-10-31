import { Injectable, inject } from '@angular/core';
import { ApiService } from './api.service';
import { Observable } from 'rxjs';
import {
  AdminHomeSection,
  UpsertHomeSectionRequest,
  HomeSectionItem,
  HomeSectionItemUpsert,
  UpsertHomeSectionItemsRequest
} from '../models/admin-home.models';

@Injectable({ providedIn: 'root' })
export class AdminHomeService {
  private api = inject(ApiService);

  list(): Observable<AdminHomeSection[]> {
    return this.api.get<AdminHomeSection[]>('/admin/home-sections');
  }

  create(payload: UpsertHomeSectionRequest) {
    return this.api.post<AdminHomeSection>('/admin/home-sections', payload);
  }

  update(id: number, payload: UpsertHomeSectionRequest) {
    return this.api.put<void>(`/admin/home-sections/${id}`, payload);
  }

  delete(id: number) {
    return this.api.delete<void>(`/admin/home-sections/${id}`);
  }

  listItems(sectionId: number): Observable<HomeSectionItem[]> {
    return this.api.get<HomeSectionItem[]>(`/admin/home-sections/${sectionId}/items`);
  }

  saveItems(sectionId: number, items: HomeSectionItemUpsert[]) {
    const payload: UpsertHomeSectionItemsRequest = { items };
    return this.api.put<void>(`/admin/home-sections/${sectionId}/items`, payload);
  }
}
