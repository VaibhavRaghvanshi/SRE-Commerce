import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const userManagementGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isLoggedIn() && auth.isAdmin()
    ? true
    : inject(Router).createUrlTree(['/admin']);
};
