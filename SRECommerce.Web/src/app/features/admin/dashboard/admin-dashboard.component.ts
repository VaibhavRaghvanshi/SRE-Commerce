import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AdminHomeService } from '../../../core/services/admin-home.service';
import { OrderService } from '../../../core/services/order.service';
import { AuthService } from '../../../core/services/auth.service';
import { AdminHomeSection } from '../../../core/models/admin-home.models';
import { AdminSummary } from '../../../core/models/order.models';

@Component({
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './admin-dashboard.component.html'
})
export class AdminDashboardComponent {
  auth = inject(AuthService);
  private homeSvc = inject(AdminHomeService);
  private orderSvc = inject(OrderService);

  loading = signal(true);
  sections = signal<AdminHomeSection[]>([]);
  summary = signal<AdminSummary | null>(null);
  error = signal<string | null>(null);

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.error.set(null);

    this.homeSvc.list().subscribe({
      next: x => {
        this.sections.set((x || []).slice().sort((a, b) => a.sortOrder - b.sortOrder));
        this.loading.set(false);
      },
      error: e => {
        this.error.set(e?.message || 'Failed to load home sections');
        this.loading.set(false);
      }
    });

    this.orderSvc.adminSummary().subscribe({
      next: x => this.summary.set(x),
      error: e => this.error.set(e?.message || 'Failed to load sales summary')
    });
  }

  enabledCount(): number {
    return this.sections().filter(s => s.isEnabled).length;
  }
}
