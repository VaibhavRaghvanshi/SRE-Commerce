import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CatalogEditor } from '../../../core/services/merchant-catalog.service';

@Component({ selector: 'app-product-translations', standalone: true, imports: [CommonModule, FormsModule],
  template: `<section aria-label="Product translations"><h3>Product translations</h3>
    <p>Add your own translated content. Customers see the published translation when available, with the base product as fallback.</p>
    <label>Translation language <select aria-label="Translation language" [(ngModel)]="language" [ngModelOptions]="{standalone:true}" [disabled]="disabled"><option *ngFor="let l of languages" [value]="l.code">{{l.name}}</option></select></label>
    <button *ngIf="!translation" type="button" (click)="add()" [disabled]="disabled">Add translation</button>
    <fieldset *ngIf="translation as t" [disabled]="disabled"><label>Translated name<input aria-label="Translated name" [(ngModel)]="t.name" [ngModelOptions]="{standalone:true}" maxlength="160" required /></label>
      <label>Translated description<textarea aria-label="Translated description" [(ngModel)]="t.description" [ngModelOptions]="{standalone:true}" maxlength="10000" rows="3"></textarea></label>
      <label>Translated SEO title<input [(ngModel)]="t.seoTitle" [ngModelOptions]="{standalone:true}" maxlength="160" /></label>
      <label>Translated SEO description<input [(ngModel)]="t.seoDescription" [ngModelOptions]="{standalone:true}" maxlength="320" /></label>
      <button type="button" (click)="remove()">Remove this translation from draft</button></fieldset>
    <p>Save the product draft, then publish to update the storefront. Hindi and English are available in the current storefront language selector.</p></section>`,
  styles: [`section{border-top:1px solid #ccd9ce;margin-top:1rem;padding-top:.6rem}p{font-size:.8rem;color:#526558;line-height:1.6}label{display:block;margin:.6rem 0;font-size:.85rem}input,textarea,select{display:block;width:100%;max-width:100%;box-sizing:border-box;border:1px solid #9cafa1;border-radius:6px;padding:.6rem;font:inherit;margin-top:.3rem}fieldset{border:0;padding:0}button{border:1px solid #9cafa1;border-radius:6px;padding:.6rem;background:white;color:#193027}`] })
export class ProductTranslationsComponent {
  @Input({required:true}) editor!: CatalogEditor;
  @Input() disabled = false;
  language = 'hi';
  readonly languages = [{code:'hi',name:'हिन्दी'},{code:'en',name:'English'},{code:'gu',name:'ગુજરાતી'},{code:'mr',name:'मराठी'},
    {code:'ta',name:'தமிழ்'},{code:'te',name:'తెలుగు'},{code:'bn',name:'বাংলা'},{code:'kn',name:'ಕನ್ನಡ'}];
  get translation() { return this.editor.translations?.[this.language]; }
  add() { this.editor.translations = { ...this.editor.translations, [this.language]: { name:'',description:'',seoTitle:'',seoDescription:'' } }; }
  remove() { if (this.editor.translations) delete this.editor.translations[this.language]; }
}
