import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { CommerceSessionService } from '../services/commerce-session.service';
export const commerceAuthInterceptor: HttpInterceptorFn = (request, next) => {
  const resource = /^\/commerce\/(catalog|tenant|theme|storefront|identity)\//.test(request.url);
  if (!resource || request.url.includes('/public/')) return next(request);
  const token = inject(CommerceSessionService).token;
  return next(token ? request.clone({ setHeaders: { Authorization: 'Bearer ' + token } }) : request);
};
