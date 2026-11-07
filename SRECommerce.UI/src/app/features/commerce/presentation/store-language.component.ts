import { Component, inject } from '@angular/core';
import { StoreLanguageService } from '../../../core/services/store-language.service';

@Component({ selector: 'app-store-language', standalone: true,
  template: `<label>{{locale.text('Language')}} <select aria-label="Store language" [value]="locale.language()" (change)="select($event)"><option value="en">English</option><option value="hi">हिन्दी</option></select></label>`,
  styles: [`:host{display:inline-block}label{display:flex;align-items:center;gap:.5rem;font:inherit;font-size:.8rem}select{font:inherit;background:var(--color-surface,#fff);color:var(--color-text,#193027);border:1px solid var(--color-border,#b8c7b9);border-radius:6px;padding:.45rem .6rem;min-height:36px}`] })
export class StoreLanguageComponent {
  readonly locale = inject(StoreLanguageService);
  select(event: Event) { this.locale.setLanguage((event.target as HTMLSelectElement).value); }
}
