import { Directive, effect, inject, input, signal } from '@angular/core';
import { forkJoin, fromEvent, merge, startWith, switchMap } from 'rxjs';
import { CommerceTenant } from '../../../core/models/commerce.models';
import { CommercePresentationService, Layout, safeThemeVariables } from '../../../core/services/commerce-presentation.service';

@Directive({ selector: '[commerceAppearance]', standalone: true, exportAs: 'commerceAppearance', host: { '[style]': 'variables()' } })
export class StoreAppearanceDirective {
  readonly store = input.required<CommerceTenant | null>({ alias: 'commerceAppearance' });
  readonly page = input('home', { alias: 'commercePage' });
  private readonly api = inject(CommercePresentationService);
  readonly layout = signal<Layout | null>(null);
  readonly variables = signal<Record<string, string>>({});
  readonly mobile = signal(false);
  constructor() {
    effect(onCleanup => {
      const store = this.store(), page = this.page();
      this.layout.set(null); this.variables.set({});
      if (!store || typeof window === 'undefined' || typeof matchMedia !== 'function') return;
      const width = matchMedia('(max-width: 650px)'), dark = matchMedia('(prefers-color-scheme: dark)');
      const sub = merge(fromEvent(width, 'change'), fromEvent(dark, 'change')).pipe(startWith(null), switchMap(() => {
        this.mobile.set(width.matches);
        return forkJoin({ layout: this.api.layout(store, page), theme: this.api.theme(store, dark.matches ? 'dark' : 'light', width.matches) }).pipe(
          switchMap(value => {
            const preferred = value.theme?.document?.defaultMode;
            return preferred && preferred !== 'system' && (preferred === 'dark') !== dark.matches
              ? forkJoin({ layout: [value.layout], theme: this.api.theme(store, preferred, width.matches) }) : [value];
          }));
      })).subscribe(value => {
        this.layout.set(value.layout?.tenantId === store.tenantId && value.layout.slug === page && value.layout.document.schemaVersion === 1 ? value.layout.document : null);
        this.variables.set(value.theme?.tenantId === store.tenantId ? safeThemeVariables(value.theme.variables) : {});
      });
      onCleanup(() => sub.unsubscribe());
    });
  }
}
