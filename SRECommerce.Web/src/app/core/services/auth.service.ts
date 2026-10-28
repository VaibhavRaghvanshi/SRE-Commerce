import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { tap } from 'rxjs/operators';
import { ApiService } from './api.service';
import {
  AuthResponse,
  LoginRequest,
  RegisterRequest,
  MeResponse
} from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private api = inject(ApiService);

  private tokenKey = 'sre_token';
  private nameKey = 'sre_user_name';
  private roleKey = 'sre_user_role';

  private _user?: MeResponse | null;

  private hasStorage(): boolean {
    return typeof window !== 'undefined' && typeof localStorage !== 'undefined';
  }

  // --- token helpers ---

  get token(): string | null {
    if (!this.hasStorage()) return null;
    return localStorage.getItem(this.tokenKey);
  }

  private setToken(token: string | null) {
    if (!this.hasStorage()) return;
    if (token) localStorage.setItem(this.tokenKey, token);
    else localStorage.removeItem(this.tokenKey);
  }

  // --- public helpers used in template ---

  isLoggedIn(): boolean {
    return !!this.token;
  }

  get userName(): string | null {
    if (this._user) return this._user.fullName;
    if (!this.hasStorage()) return null;
    return localStorage.getItem(this.nameKey);
  }

  get userRole(): string | null {
    if (this._user) return this._user.role;
    if (!this.hasStorage()) return null;
    return localStorage.getItem(this.roleKey);
  }

  isAdmin(): boolean {
    return (this.userRole || '').toLowerCase() === 'admin';
  }

  isEmployee(): boolean {
    return (this.userRole || '').toLowerCase() === 'employee';
  }

  isStaff(): boolean {
    return this.isAdmin() || this.isEmployee();
  }

  logout() {
    this.setToken(null);
    if (this.hasStorage()) {
      localStorage.removeItem(this.nameKey);
      localStorage.removeItem(this.roleKey);
    }
    this._user = null;
  }

  // --- API calls ---

  login(payload: LoginRequest): Observable<AuthResponse> {
    return this.api.post<AuthResponse>('/auth/login', payload).pipe(
      tap(res => this.handleAuth(res))
    );
  }

  register(payload: RegisterRequest): Observable<AuthResponse> {
    return this.api.post<AuthResponse>('/auth/register', payload).pipe(
      tap(res => this.handleAuth(res))
    );
  }

  loadCurrentUser(): Observable<MeResponse | null> {
    if (!this.token) return of(null);

    return this.api.get<MeResponse>('/auth/me').pipe(
      tap(user => {
        this._user = user;
        if (this.hasStorage()) {
          localStorage.setItem(this.nameKey, user.fullName);
          localStorage.setItem(this.roleKey, user.role);
        }
      })
    );
  }

  // --- internal ---

  private handleAuth(res: AuthResponse) {
    this.setToken(res.token);

    this._user = {
      id: 0, // /auth/me se real id milegi
      fullName: res.fullName,
      email: res.email,
      role: res.role
    };

    if (this.hasStorage()) {
      localStorage.setItem(this.nameKey, res.fullName);
      localStorage.setItem(this.roleKey, res.role);
    }
  }
}
