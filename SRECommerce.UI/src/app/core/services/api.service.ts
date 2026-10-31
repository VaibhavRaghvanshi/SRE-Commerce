import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { timeout } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly BASE_URL = environment.apiBase;
  private readonly READ_TIMEOUT_MS = 15000;

  constructor(private http: HttpClient) {}

  get<T>(url: string, params?: any) {
    return this.http.get<T>(this.BASE_URL + url, { params: this.cleanParams(params) })
      .pipe(timeout({ first: this.READ_TIMEOUT_MS }));
  }

  post<T>(url: string, body: any, params?: any) {
    return this.http.post<T>(this.BASE_URL + url, body, { params: this.cleanParams(params) });
  }

  put<T>(url: string, body: any, params?: any) {
    return this.http.put<T>(this.BASE_URL + url, body, { params: this.cleanParams(params) });
  }

  delete<T>(url: string, params?: any) {
    return this.http.delete<T>(this.BASE_URL + url, { params: this.cleanParams(params) });
  }

  uploadImage(file: File) {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<{ url: string }>(this.BASE_URL + '/admin/uploads', form);
  }

  private cleanParams(params?: any) {
    if (!params) return undefined;

    return Object.fromEntries(
      Object.entries(params)
        .filter(([, value]) => value !== undefined && value !== null && value !== '')
        .map(([key, value]) => [key, String(value)])
    );
  }
}
