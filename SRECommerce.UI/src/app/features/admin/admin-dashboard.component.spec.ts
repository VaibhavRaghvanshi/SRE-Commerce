import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminDashboardComponent } from './dashboard/admin-dashboard.component';
import { AdminHomeService } from '../../core/services/admin-home.service';
import { OrderService } from '../../core/services/order.service';
import { AuthService } from '../../core/services/auth.service';

describe('Routed AdminDashboardComponent', () => {
  let summary: jasmine.Spy;
  beforeEach(async () => {
    summary = jasmine.createSpy('adminSummary').and.returnValue(of({totalRevenue: 240, totalOrders: 1, topProducts: [], recentOrders: []}));
    await TestBed.configureTestingModule({
      imports: [AdminDashboardComponent],
      providers: [provideZonelessChangeDetection(), provideRouter([]),
        {provide: AdminHomeService, useValue: {list: () => of([])}},
        {provide: OrderService, useValue: {adminSummary: summary}},
        {provide: AuthService, useValue: {isAdmin: () => false}}]
    }).compileComponents();
  });

  it('renders actual sales data and hides account-management access for employees', () => {
    const fixture = TestBed.createComponent(AdminDashboardComponent);
    fixture.detectChanges();
    expect(summary).toHaveBeenCalledTimes(1);
    expect(fixture.nativeElement.textContent).toContain('240');
    expect(fixture.nativeElement.querySelector('a[href="/admin/users"]')).toBeNull();
  });

  it('shows the summary error when the API fails', () => {
    summary.and.returnValue(throwError(() => new Error('Summary unavailable')));
    const fixture = TestBed.createComponent(AdminDashboardComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Summary unavailable');
  });
});
