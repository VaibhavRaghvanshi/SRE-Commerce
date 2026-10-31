import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { AccountComponent } from './account.component';
import { OrderService } from '../../core/services/order.service';
import { AuthService } from '../../core/services/auth.service';
import { Order } from '../../core/models/order.models';

describe('AccountComponent order history', () => {
  let mine: jasmine.Spy;
  beforeEach(async () => {
    mine = jasmine.createSpy('mine').and.returnValue(of([]));
    await TestBed.configureTestingModule({
      imports: [AccountComponent],
      providers: [provideZonelessChangeDetection(),
        {provide: OrderService, useValue: {mine}},
        {provide: AuthService, useValue: {userName: 'Customer'}}]
    }).compileComponents();
  });

  it('shows the empty order state after a successful empty response', () => {
    const fixture = TestBed.createComponent(AccountComponent);
    fixture.detectChanges();
    expect(mine).toHaveBeenCalledTimes(1);
    expect(fixture.nativeElement.textContent).toContain('No orders yet.');
    expect(fixture.nativeElement.textContent).not.toContain('Loading orders...');
  });

  it('shows loading until the order response arrives', () => {
    const response = new Subject<Order[]>();
    mine.and.returnValue(response);
    const fixture = TestBed.createComponent(AccountComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading orders...');
    response.next([]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No orders yet.');
  });
});
