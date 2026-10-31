import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { environment } from '../../../environments/environment';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const base = environment.apiBase.replace(/\/$/, '');
  const targetsApi = req.url === base || req.url.startsWith(base + '/') || req.url.startsWith(base + '?');
  if (!targetsApi) {
    return next(req);
  }
  const token = inject(AuthService).token;
  if (!token) {
    return next(req);
  }

  const cloned = req.clone({
    setHeaders: {
      Authorization: `Bearer ${token}`
    }
  });

  return next(cloned);
};
