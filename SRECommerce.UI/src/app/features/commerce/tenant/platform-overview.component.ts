import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom, timeout } from 'rxjs';

interface Count { code: string; count: number; }
interface Overview {
  sampledAt: string; totalTenants: number; effectiveSubscriptions: number; expiringNextSevenDays: number; activePlans: number;
  tenantStates: { status: number; count: number }[]; businessTypes: Count[]; configuredModules: Count[];
  registrations: { date: string; count: number }[];
}
interface Health { sampledAt: string; services: { service: string; status: string; elapsedMilliseconds: number | null }[]; }
interface PlatformAudit { id: number; actorUserId: string; action: string; resource: string; createdAt: string; }

@Component({ selector: 'app-platform-overview', standalone: true, imports: [CommonModule, FormsModule],
  templateUrl: './platform-overview.component.html', styleUrl: './platform-overview.component.scss' })
export class PlatformOverviewComponent {
  private readonly http = inject(HttpClient);
  readonly plansChanged = output<void>();
  readonly overview = signal<Overview | null>(null); readonly health = signal<Health | null>(null);
  readonly loading = signal(false); readonly checking = signal(false); readonly creating = signal(false);
  readonly audit = signal<PlatformAudit[] | null>(null); readonly historyLoading = signal(false); readonly historyError = signal(''); auditPage = 1;
  readonly error = signal(''); readonly healthError = signal(''); readonly planMessage = signal('');
  readonly states = ['Draft', 'Pending approval', 'Active', 'Suspended', 'Archived'];
  readonly modules = ['themes', 'storefront', 'catalog', 'inventory', 'pricing', 'cart', 'orders', 'payments', 'shipping', 'notifications', 'manufacturing', 'discovery'];
  plan = { code: '', name: '', maxLocations: 1, maxDomains: 0, maxUsers: 5, modules: ['catalog', 'storefront'] };
  constructor() { if (typeof window !== 'undefined') void this.refresh(); }
  async refresh() {
    if (this.loading()) return; this.loading.set(true); this.error.set('');
    try { this.overview.set(await firstValueFrom(this.http.get<Overview>('/commerce/tenant/platform/overview').pipe(timeout(12000)))); }
    catch { this.error.set('Platform analytics could not be loaded. Check your administrator access and try again.'); }
    finally { this.loading.set(false); }
  }
  async checkHealth() {
    if (this.checking()) return; this.checking.set(true); this.healthError.set('');
    try { this.health.set(await firstValueFrom(this.http.get<Health>('/commerce/tenant/platform/service-health').pipe(timeout(12000)))); }
    catch (error) { this.healthError.set(error instanceof HttpErrorResponse && error.status === 429 ? 'Wait a minute before checking service health again. The last sample may be out of date.' : 'Service status could not be checked. The last sample may be out of date.'); }
    finally { this.checking.set(false); }
  }
  selectModule(code: string, selected: boolean) {
    this.plan.modules = selected ? [...new Set([...this.plan.modules, code])] : this.plan.modules.filter(item => item !== code);
  }
  async loadAudit(page = 1) {
    if (this.historyLoading() || page < 1 || page > 10000) return;
    this.historyLoading.set(true); this.historyError.set('');
    try { this.audit.set(await firstValueFrom(this.http.get<PlatformAudit[]>('/commerce/tenant/platform/audit', { params: { page } }).pipe(timeout(12000)))); this.auditPage = page; }
    catch { this.historyError.set('Platform audit history could not be loaded.'); }
    finally { this.historyLoading.set(false); }
  }
  async createPlan() {
    if (this.creating()) return; this.creating.set(true); this.planMessage.set('');
    const body = { ...this.plan, code: this.plan.code.trim().toLowerCase(), name: this.plan.name.trim(), modules: [...this.plan.modules] };
    try {
      await firstValueFrom(this.http.post<void>('/commerce/tenant/platform/plans', body).pipe(timeout(12000)));
      this.plan = { code: '', name: '', maxLocations: 1, maxDomains: 0, maxUsers: 5, modules: ['catalog', 'storefront'] };
      this.planMessage.set('Plan created. Existing tenant subscriptions were preserved.'); this.plansChanged.emit(); await this.refresh();
    } catch (error) {
      this.planMessage.set(error instanceof HttpErrorResponse && error.status === 409 ? 'This plan code already exists. Refresh the plan list to check the saved result.' : 'Plan creation could not be confirmed. Refresh plans and check this code before retrying.');
    } finally { this.creating.set(false); }
  }
}
