import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, Input, Output, EventEmitter, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { CatalogDefinition, MerchantCatalogService } from '../../../core/services/merchant-catalog.service';

@Component({ selector: 'app-catalog-fields', standalone: true, imports: [CommonModule, FormsModule], template: `
<section class="fields-panel"><header><div><p class="eyebrow">Your product information</p><h2>Product fields</h2><p>Create business-specific fields, then assign them to categories below. Existing field codes and types stay fixed.</p></div><button type="button" class="secondary" (click)="select('')" [disabled]="busy()">New field</button></header>
<p role="alert" *ngIf="error()">{{error()}}</p><p role="status" *ngIf="message()">{{message()}}</p>
<label>Choose a field<select name="field-selection" [ngModel]="id" (ngModelChange)="select($event)" [disabled]="busy()"><option value="">Create a field</option><option *ngFor="let field of definitions" [value]="field.id">{{field.name}}{{field.archived ? ' · Archived' : ''}}</option></select></label>
<form #form="ngForm" (ngSubmit)="save()"><fieldset [disabled]="busy()"><legend>{{id ? 'Edit field' : 'New field'}}</legend><div class="fields-grid">
  <label>Name<input name="field-name" [(ngModel)]="draft.name" maxlength="160" required /></label><label>Code<input name="field-code" [(ngModel)]="draft.code" pattern="[a-z0-9]+(-[a-z0-9]+)*" maxlength="80" [readonly]="!!id" required /></label>
  <label>Type<select name="field-type" [(ngModel)]="draft.dataType" [disabled]="!!id"><option [ngValue]="0">Text</option><option [ngValue]="1">Number</option><option [ngValue]="2">Yes / no</option><option [ngValue]="3">Date</option><option [ngValue]="4">Choice</option></select></label>
  <label>Unit (optional)<input name="field-unit" [(ngModel)]="draft.unit" maxlength="30" placeholder="g, kg, cm" /></label></div>
  <label *ngIf="draft.dataType === 4">Choices, one per line<textarea name="field-options" [(ngModel)]="options" rows="4" maxlength="8100" required></textarea></label>
  <label class="check" *ngIf="id"><input name="field-archived" type="checkbox" [(ngModel)]="draft.archived" />Archive field</label><p *ngIf="draft.archived">Unassign this field from every category before archiving it. Current published product snapshots are preserved.</p>
</fieldset><button [disabled]="busy() || form.invalid">{{busy() ? 'Saving…' : id ? 'Save field' : 'Create field'}}</button></form></section>`,
styles: [`.fields-panel{background:#fff;border:1px solid #d4e0e9;border-radius:14px;padding:1.5rem;margin:1.5rem 0;color:#203549}header{display:flex;justify-content:space-between;gap:1rem;align-items:start;flex-wrap:wrap}h2{margin:.3rem 0}.eyebrow{font-size:.7rem;text-transform:uppercase;letter-spacing:.14em;color:#536d80}p{font-size:.85rem;line-height:1.6;max-width:65ch}label{display:block;font-size:.85rem;margin:.8rem 0}input:not([type=checkbox]),select,textarea{display:block;width:100%;margin-top:.4rem;padding:.7rem;border:1px solid #91a8ba;border-radius:7px;color:#203549;background:white;font:inherit}select[name=field-selection]{max-width:30rem}.fields-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:0 1rem}fieldset{border:1px solid #d4e0e9;border-radius:8px;margin:1rem 0;padding:1rem}legend{font-size:.85rem;font-weight:600}.check{display:flex;align-items:center;gap:.5rem}.check input{width:1rem;height:1rem}button{padding:.7rem 1rem;border:1px solid #214e6e;border-radius:7px;background:#214e6e;color:#fff;cursor:pointer}.secondary{background:white;color:#214e6e}button:disabled{opacity:.5}[role=alert]{color:#96291e}[role=status]{color:#205f3d}@media(max-width:550px){.fields-grid{grid-template-columns:1fr}.fields-panel{padding:1rem}}`] })
export class CatalogFieldsComponent {
  @Input({ required: true }) tenant = ''; @Input({ required: true }) definitions: CatalogDefinition[] = []; @Input() supportReason = '';
  @Output() changed = new EventEmitter<void>(); private readonly api = inject(MerchantCatalogService);
  readonly busy = signal(false); readonly error = signal(''); readonly message = signal(''); id = ''; options = '';
  draft = this.blank(); private blank() { return { code: '', name: '', dataType: 0, unit: '', version: null as string | null, archived: false }; }
  select(id: string) {
    this.id = id; this.error.set(''); this.message.set('');
    const field = this.definitions.find(row => row.id === id); this.draft = field ? { code: field.code, name: field.name, dataType: field.dataType, unit: field.unit || '', version: field.version || null, archived: !!field.archived } : this.blank();
    this.options = field?.options.join('\n') || '';
  }
  async save() {
    if (this.busy()) return; this.error.set(''); this.message.set('');
    const choices = this.draft.dataType === 4 ? this.options.split(/\r?\n/).map(value => value.trim()).filter(Boolean) : [];
    if (choices.length > 100 || new Set(choices).size !== choices.length || this.draft.dataType === 4 && !choices.length) { this.error.set('Choose 1–100 different non-empty options.'); return; }
    if (this.id && !this.draft.version) { this.error.set('Refresh the catalog before editing this field.'); return; }
    this.busy.set(true);
    try { await firstValueFrom(this.api.saveDefinition(this.tenant, this.id, { ...this.draft, options: choices, unit: this.draft.unit.trim() || null }, this.supportReason)); this.select(''); this.message.set('Product field saved.'); this.changed.emit(); }
    catch (error) { this.error.set(error instanceof HttpErrorResponse && error.error?.title === 'UnassignAttributeBeforeArchive' ? 'Unassign this field from all categories before archiving it.' : error instanceof HttpErrorResponse && error.status === 409 ? 'The field changed or is still in use. Refresh before retrying.' : 'The field could not be saved. Check the values and your access.'); }
    finally { this.busy.set(false); }
  }
}
