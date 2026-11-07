import { CommonModule } from '@angular/common';
import { Component, DestroyRef, Input, OnChanges, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { CommerceOrder } from '../../../core/models/commerce-order.models';
import { GstWorksheet, GstWorksheetRequest } from '../../../core/models/commerce-gst.models';
import { MerchantOrderService } from '../../../core/services/merchant-order.service';

@Component({
  selector: 'app-gst-worksheet', standalone: true, imports: [CommonModule, FormsModule],
  template: `<details>
    <summary>GST preparation worksheet</summary>
    <p>Prepare a draft for your accounting team. Verify the classifications and place of supply before issuing an invoice.</p>
    <form #form="ngForm" *ngIf="input as draft" (ngSubmit)="preview()" (input)="result.set(null)" (change)="result.set(null)">
      <fieldset [disabled]="busy()">
        <legend>Supplier</legend>
        <label>Legal name<input name="supplier-name" [(ngModel)]="draft.supplier.name" maxlength="160" required /></label>
        <label>Billing address<input name="supplier-address" [(ngModel)]="draft.supplier.address" maxlength="600" required /></label>
        <label>GSTIN<input name="supplier-gstin" [(ngModel)]="draft.supplier.gstin" maxlength="15" minlength="15" required /></label>
      </fieldset>
      <fieldset [disabled]="busy()">
        <legend>Recipient and supply</legend>
        <label>Billing name<input name="recipient-name" [(ngModel)]="draft.recipient.name" maxlength="160" required /></label>
        <label>Billing address<input name="recipient-address" [(ngModel)]="draft.recipient.address" maxlength="600" required /></label>
        <label>Recipient GSTIN, if registered<input name="recipient-gstin" [(ngModel)]="draft.recipient.gstin" maxlength="15" /></label>
        <label>Place of supply<input name="place-name" [(ngModel)]="draft.placeOfSupplyName" maxlength="80" required /></label>
        <label>Place of supply code<input name="place-code" [(ngModel)]="draft.placeOfSupplyCode" inputmode="numeric" pattern="[0-9]{2}" maxlength="2" required /></label>
        <label class="check"><input name="reverse-charge" type="checkbox" [(ngModel)]="draft.reverseCharge" /> Reverse charge</label>
      </fieldset>
      <fieldset [disabled]="busy()">
        <legend>Classification</legend>
        <div class="item" *ngFor="let item of draft.items; let i = index">
          <strong>{{order.items[i].sku}} · {{order.items[i].name}}</strong>
          <label>HSN<input [name]="'hsn-' + i" [(ngModel)]="item.hsn" inputmode="numeric" pattern="(?:[0-9]{4}|[0-9]{6}|[0-9]{8})" maxlength="8" required /></label>
          <label>Unit code<input [name]="'unit-' + i" [(ngModel)]="item.unitCode" pattern="[A-Za-z]{3}" maxlength="3" required /></label>
        </div>
        <label *ngIf="order.shippingTotal > 0">Delivery SAC<input name="shipping-sac" [(ngModel)]="draft.shippingSac" inputmode="numeric" pattern="[0-9]{6}" maxlength="6" required /></label>
      </fieldset>
      <button [disabled]="busy() || form.invalid" type="submit">{{busy() ? 'Preparing…' : 'Preview worksheet'}}</button>
    </form>
    <p role="alert" *ngIf="error()">{{error()}}</p>
    <section *ngIf="result() as worksheet" aria-live="polite">
      <h4>Draft worksheet</h4>
      <p>{{worksheet.orderNumber}} · {{worksheet.placeOfSupplyName}} ({{worksheet.placeOfSupplyCode}})</p>
      <ul><li *ngFor="let line of worksheet.lines">{{line.sku}} · HSN {{line.classification}} · {{line.quantity}} {{line.unitCode}} · {{line.total | currency:worksheet.currency}}</li></ul>
      <p>Delivery {{worksheet.shipping.total | currency:worksheet.currency}} · Recorded tax {{worksheet.recordedTax | currency:worksheet.currency}}</p>
      <p><strong>Order total {{worksheet.total | currency:worksheet.currency}}</strong></p>
      <button type="button" (click)="download(worksheet)">Download draft JSON</button>
    </section>
  </details>`,
  styles: [`details{border-top:1px solid #c7d4cd;margin-top:1rem;padding-top:1rem}summary{cursor:pointer;font-weight:650}p{font-size:.86rem;line-height:1.6}
    fieldset{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:.65rem;border:1px solid #ccd9d1;border-radius:8px;padding:.8rem;margin:1rem 0;min-width:0}
    label{display:block;font-size:.82rem}input:not([type=checkbox]){display:block;width:100%;box-sizing:border-box;padding:.65rem;border:1px solid #9eafa5;border-radius:6px;font:inherit;margin-top:.25rem}
    .item{grid-column:1/-1;display:grid;grid-template-columns:1fr 1fr;gap:.5rem}.item strong{grid-column:1/-1;overflow-wrap:anywhere}.check{align-self:end}
    button{border:1px solid #16394e;background:#16394e;color:white;border-radius:6px;padding:.65rem .8rem}button:disabled{opacity:.55}section{margin-top:1rem;padding:.8rem;background:#edf3ee;border-radius:8px;overflow-wrap:anywhere}
    @media(max-width:600px){fieldset{grid-template-columns:1fr}}`]
})
export class GstWorksheetComponent implements OnChanges {
  @Input({ required: true }) tenant!: string;
  @Input({ required: true }) order!: CommerceOrder;
  @Input() supportReason = '';
  private readonly api = inject(MerchantOrderService);
  private readonly destroy = inject(DestroyRef);
  private revision = 0;
  input: GstWorksheetRequest | null = null;
  readonly result = signal<GstWorksheet | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');

  ngOnChanges() {
    this.revision++;
    this.result.set(null);
    this.error.set('');
    this.busy.set(false);
    const contact = this.order.contact;
    this.input = { version: this.order.version, supplier: { name: '', address: '', gstin: '' },
      recipient: { name: contact.fullName, address: [contact.addressLine1, contact.addressLine2, contact.city, contact.postalCode].filter(Boolean).join(', '), gstin: null },
      placeOfSupplyCode: '', placeOfSupplyName: '', reverseCharge: false, shippingSac: null,
      items: this.order.items.map(item => ({ variantId: item.variantId, hsn: '', unitCode: '' })) };
  }

  async preview() {
    if (!this.input || this.busy()) return;
    const revision = this.revision;
    this.busy.set(true);
    this.result.set(null);
    this.error.set('');
    try {
      const value = await firstValueFrom(this.api.gstWorksheet(this.tenant, this.order.id, this.input, this.supportReason).pipe(takeUntilDestroyed(this.destroy)));
      if (revision === this.revision) this.result.set(value);
    } catch (error) {
      if (revision === this.revision) this.error.set(error instanceof HttpErrorResponse && error.status === 409
        ? 'This order changed or is outside the worksheet scope. Reload it and check its status.'
        : 'The worksheet could not be prepared. Check the GSTIN, classifications and place of supply.');
    } finally {
      if (revision === this.revision) this.busy.set(false);
    }
  }

  download(worksheet: GstWorksheet) {
    const url = URL.createObjectURL(new Blob([JSON.stringify(worksheet, null, 2)], { type: 'application/json' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = `gst-worksheet-${this.order.id}.json`;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 0);
  }
}
