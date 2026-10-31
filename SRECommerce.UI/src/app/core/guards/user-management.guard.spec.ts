import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { ActivatedRouteSnapshot, provideRouter, RouterStateSnapshot, UrlTree } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { userManagementGuard } from './user-management.guard';

describe('User management route authorization', () => {
  function evaluate(loggedIn: boolean, admin: boolean) {
    TestBed.configureTestingModule({providers: [provideZonelessChangeDetection(), provideRouter([]),
      {provide: AuthService, useValue: {isLoggedIn: () => loggedIn, isAdmin: () => admin}}]});
    return TestBed.runInInjectionContext(() => userManagementGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot));
  }

  it('allows administrators', () => expect(evaluate(true, true)).toBeTrue());
  it('redirects employees', () => expect(evaluate(true, false) instanceof UrlTree).toBeTrue());
  it('redirects logged-out users', () => expect(evaluate(false, false) instanceof UrlTree).toBeTrue());
});
