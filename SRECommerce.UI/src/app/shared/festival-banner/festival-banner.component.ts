import { Component, DestroyRef, afterNextRender, computed, inject, signal } from '@angular/core';
import { StoreLanguageService } from '../../core/services/store-language.service';
import { activeFestival, festivalCountry, festivalGreetings } from './festival-calendar';

@Component({
  selector: 'app-festival-banner',
  standalone: true,
  template: `
    @if (greeting(); as wish) {
      <aside class="festival" [attr.data-festival]="festival()" [attr.lang]="language()" aria-label="Festival greeting">
        <span class="festival-icon" aria-hidden="true">{{ wish.icon }}</span>
        <p><strong>{{ wish[language()] }}</strong>
          <span>{{ language() === 'hi' ? 'आपको और आपके परिवार को स्नेह भरी शुभकामनाएँ।' : 'Warm wishes to you and your loved ones.' }}</span>
        </p>
      </aside>
    }
  `,
  styles: [`
    :host { display: block; }
    .festival {
      display: flex; justify-content: center; align-items: center; gap: .7rem;
      padding: .65rem 1rem; text-align: start; color: #252c52;
      background: #f1efff; border-bottom: 1px solid #ded8f3;
    }
    .festival[data-festival="diwali"] { background: #fff1d3; color: #642c10; border-color: #edce93; }
    .festival[data-festival="holi"] { background: #fcebf8; color: #5d2054; border-color: #efc6e7; }
    .festival[data-festival="christmas"] { background: #e6f5e9; color: #174432; border-color: #b8ddc1; }
    .festival-icon { flex: none; font-size: 1.45rem; line-height: 1; }
    p { margin: 0; display: flex; flex-wrap: wrap; gap: .15rem .7rem; align-items: baseline; line-height: 1.35; }
    strong { font-size: .97rem; }
    p span { font-size: .85rem; }
  `],
})
export class FestivalBannerComponent {
  private readonly destroy = inject(DestroyRef);
  readonly language = inject(StoreLanguageService).language;
  readonly festival = signal<ReturnType<typeof activeFestival>>(undefined);
  readonly greeting = computed(() => {
    const festival = this.festival();
    return festival ? festivalGreetings[festival] : undefined;
  });

  constructor() {
    afterNextRender(() => {
      const refresh = () => {
        const country = festivalCountry(Intl.DateTimeFormat().resolvedOptions().timeZone, navigator.languages);
        this.festival.set(activeFestival(new Date(), country));
      };
      refresh();
      const timer = window.setInterval(refresh, 60_000);
      window.addEventListener('focus', refresh);
      this.destroy.onDestroy(() => {
        window.clearInterval(timer);
        window.removeEventListener('focus', refresh);
      });
    });
  }
}
