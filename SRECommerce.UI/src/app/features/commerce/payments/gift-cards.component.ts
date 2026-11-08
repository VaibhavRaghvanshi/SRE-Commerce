import { CommonModule } from '@angular/common';
import { Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { GiftCard, GiftCardIssue, GiftCardService } from '../../../core/services/gift-card.service';

@Component({
  selector: 'app-gift-cards',
  imports: [CommonModule, FormsModule],
  templateUrl: './gift-cards.component.html',
  styleUrl: './payments-workspace.component.scss'
})
export class GiftCardsComponent {
  readonly tenant = input.required<string>();
  private readonly api = inject(GiftCardService);
  readonly rows = signal<GiftCard[]>([]);
  readonly page = signal(1);
  readonly loaded = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly issuedCode = signal('');
  readonly pending = signal<GiftCardIssue | null>(null);
  readonly voidTarget = signal<GiftCard | null>(null);
  value: number | null = null;
  reference = '';

  async load(page = 1) {
    if (this.busy() || page < 1) return;
    this.busy.set(true);
    this.error.set('');
    try {
      this.rows.set(await this.api.list(this.tenant(), page, true));
      this.page.set(page);
      this.loaded.set(true);
    } catch { this.error.set('Issued gift cards could not be loaded.'); }
    finally { this.busy.set(false); }
  }

  async issue() {
    if (this.busy()) return;
    let operation = this.pending();
    if (!operation) {
      if (!this.value || !Number.isInteger(this.value) || this.value < 1 || !this.reference.trim()) return;
      const code = Array.from(crypto.getRandomValues(new Uint8Array(16)), byte => byte.toString(16).padStart(2, '0')).join('').toUpperCase();
      operation = { id: crypto.randomUUID(), code, value: this.value, reference: this.reference.trim() };
      this.pending.set(operation);
      this.issuedCode.set('');
    }
    this.busy.set(true);
    this.error.set('');
    try {
      const card = await this.api.issue(this.tenant(), operation);
      this.rows.update(rows => [card, ...rows.filter(row => row.id !== card.id)].slice(0, 50));
      this.issuedCode.set(operation.code);
      this.pending.set(null);
      this.value = null;
      this.reference = '';
    } catch (error) {
      const definitive = error instanceof HttpErrorResponse && [400, 401, 403, 404, 409].includes(error.status);
      if (definitive) this.pending.set(null);
      this.error.set(definitive ? 'The gift card could not be issued. Check the value and your store access.'
        : 'Issuance could not be confirmed. Keep this page open and retry the same card.');
    } finally { this.busy.set(false); }
  }

  async voidCard(card: GiftCard) {
    if (this.busy() || this.voidTarget()?.id !== card.id) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const updated = await this.api.void(this.tenant(), card.id);
      this.rows.update(rows => rows.map(row => row.id === updated.id ? updated : row));
      this.voidTarget.set(null);
    } catch { this.error.set('The card could not be voided. Refresh to check whether it has been claimed.'); }
    finally { this.busy.set(false); }
  }
}
