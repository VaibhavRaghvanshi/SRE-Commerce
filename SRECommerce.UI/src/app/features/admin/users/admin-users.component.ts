import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';

interface AdminUser {
  id: number;
  fullName: string;
  email: string;
  role: 'Admin' | 'Employee' | 'Customer';
  createdAt: string;
}

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="admin-page">
      <div class="page-hero compact">
        <div>
          <p class="eyebrow">ACCESS CONTROL</p>
          <h1>Manage team and customer access.</h1>
          <p>Employees can sign in to the dashboard and manage catalog, orders, banners, home sections, and uploads.</p>
        </div>
        <button class="btn-primary" type="button" (click)="load()">Refresh</button>
      </div>

      <div *ngIf="message()" class="alert mb-3">{{ message() }}</div>

      <div class="admin-columns">
        <form class="panel" (ngSubmit)="create()">
          <div class="panel-head">
            <h2>Create access</h2>
            <span class="text-muted">Admin, employee, or customer</span>
          </div>

          <label>Full name</label>
          <input class="form-control" name="fullName" [(ngModel)]="form.fullName" required />

          <label>Email</label>
          <input class="form-control" name="email" type="email" [(ngModel)]="form.email" required />

          <label>Password</label>
          <input class="form-control" name="password" type="password" [(ngModel)]="form.password" required />

          <label>Role</label>
          <select class="form-control" name="role" [(ngModel)]="form.role">
            <option>Employee</option>
            <option>Admin</option>
            <option>Customer</option>
          </select>

          <button class="btn-primary w-100 mt-3" type="submit" [disabled]="saving()">
            {{ saving() ? 'Creating...' : 'Create user' }}
          </button>
        </form>

        <div class="panel">
          <div class="panel-head">
            <h2>Staff overview</h2>
            <span class="text-muted">{{ staffCount() }} dashboard users</span>
          </div>
          <div class="access-metrics">
            <div><strong>{{ countRole('Admin') }}</strong><span>Admins</span></div>
            <div><strong>{{ countRole('Employee') }}</strong><span>Employees</span></div>
            <div><strong>{{ countRole('Customer') }}</strong><span>Customers</span></div>
          </div>
          <p class="text-muted mt-3">
            Use employees for day-to-day catalog, stock, banner, home-page, and order operations.
          </p>
        </div>
      </div>

      <div class="panel mt-3">
        <div class="panel-head">
          <h2>All users</h2>
          <span class="text-muted">{{ users().length }} accounts</span>
        </div>
        <table class="table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Email</th>
              <th>Role</th>
              <th>Created</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let user of users()">
              <td>{{ user.fullName }}</td>
              <td>{{ user.email }}</td>
              <td><span class="badge">{{ user.role }}</span></td>
              <td>{{ user.createdAt | date:'mediumDate' }}</td>
              <td class="text-right">
                <button class="btn-outline" type="button" (click)="makeEmployee(user)" *ngIf="user.role === 'Customer'">
                  Make employee
                </button>
                <button class="btn-danger" type="button" (click)="remove(user)">
                  Delete
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class AdminUsersComponent {
  private api = inject(ApiService);

  users = signal<AdminUser[]>([]);
  saving = signal(false);
  message = signal<string | null>(null);

  form = {
    fullName: '',
    email: '',
    password: '',
    role: 'Employee'
  };

  ngOnInit() {
    this.load();
  }

  load() {
    this.api.get<AdminUser[]>('/admin/users').subscribe({
      next: users => this.users.set(users || []),
      error: err => this.message.set(err?.error?.message || 'Failed to load users.')
    });
  }

  create() {
    this.saving.set(true);
    this.message.set(null);
    this.api.post<AdminUser>('/admin/users', this.form).subscribe({
      next: () => {
        this.form = { fullName: '', email: '', password: '', role: 'Employee' };
        this.saving.set(false);
        this.message.set('User access created.');
        this.load();
      },
      error: err => {
        this.saving.set(false);
        this.message.set(err?.error?.message || 'Failed to create user.');
      }
    });
  }

  makeEmployee(user: AdminUser) {
    this.api.put(`/admin/users/${user.id}/role`, 'Employee').subscribe({
      next: () => this.load(),
      error: err => this.message.set(err?.error?.message || 'Failed to update role.')
    });
  }

  remove(user: AdminUser) {
    if (!confirm(`Delete ${user.fullName}?`)) return;
    this.api.delete(`/admin/users/${user.id}`).subscribe({
      next: () => this.load(),
      error: err => this.message.set(err?.error?.message || 'Failed to delete user.')
    });
  }

  countRole(role: AdminUser['role']): number {
    return this.users().filter(u => u.role === role).length;
  }

  staffCount(): number {
    return this.countRole('Admin') + this.countRole('Employee');
  }
}
