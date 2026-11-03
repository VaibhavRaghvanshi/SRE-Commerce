import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom, timeout } from 'rxjs';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
import { colorKeys, Layout, safeThemeVariables, ThemeDocument } from '../../../core/services/commerce-presentation.service';

interface Draft { id: string; name?: string; title?: string; slug?: string; archived?: boolean; version: string; document: ThemeDocument | Layout; }
interface Version { id: string; revision: number; createdAt: string; }
interface Publication { versionId: string; sequence: number; publicationVersion: string; themeVersion?: string; pageVersion?: string; }

@Component({
  selector: 'app-presentation-editor', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './presentation-editor.component.html',
  styles: [`:host{display:block;background:#f4f7fb;color:#193043;min-height:100vh}main{max-width:1100px;margin:auto;padding:2rem 1rem}
    article{margin:1rem 0;padding:1rem;background:white;border:1px solid #cfdae4;border-radius:10px}label{display:block;margin:.45rem 0}
    .fields,.colors{display:flex;flex-wrap:wrap;gap:.8rem;align-items:end}input,select,textarea{display:block;max-width:100%;padding:.45rem;border:1px solid #8199a9;border-radius:5px}
    textarea{width:min(100%,62rem);font-family:monospace}button{background:#184d72;color:white;border:0;border-radius:5px;padding:.55rem .75rem;margin:.35rem .35rem .35rem 0}button:disabled{opacity:.5}
    .preview{padding:1.4rem;border:1px solid var(--color-border,#cbd5e1);background:var(--color-background,#fff);color:var(--color-text,#182b39);font:var(--font-size,16px)/var(--line-height,1.5) var(--font-family,system-ui);border-radius:var(--border-radius,8px)}
    .preview button{background:var(--color-button-background,#184d72);color:var(--color-button-text,#fff)}[role=alert]{color:#992819}`]
})
export class PresentationEditorComponent {
  private readonly http = inject(HttpClient);
  private readonly session = inject(CommerceSessionService);
  private readonly route = inject(ActivatedRoute);
  readonly tenant = this.route.snapshot.paramMap.get('tenant') || '';
  readonly theme = this.route.snapshot.data['presentation'] === 'theme';
  readonly colors = colorKeys;
  readonly presets = ['neutral','spices','clothing','grocery','general-store','custom'];
  readonly sectionKinds = ['hero','featured-categories','featured-products','new-arrivals','best-sellers','promotion','reviews','text','product-fields'];
  readonly drafts = signal<Draft[]>([]); readonly active = signal<Draft | null>(null);
  readonly versions = signal<Version[]>([]); readonly publication = signal<Publication | null>(null);
  readonly busy = signal(false); readonly error = signal(''); readonly message = signal('');
  readonly previewTheme = signal<Record<string, string> | null>(null); readonly previewLayout = signal<Layout | null>(null);
  preset = 'neutral'; newName = ''; slug = 'home'; selected = ''; title = ''; reason = '';
  themeDocument: ThemeDocument | null = null; layout: Layout | null = null;
  colorMode: 'light' | 'dark' = 'light'; device: 'desktop' | 'mobile' = 'desktop'; advanced = false; json = '';
  get support() { return !!this.session.access()?.isPlatformAdmin; }
  get role() { const a = this.session.access(); return this.session.token && a?.selectedTenantId === this.tenant ? a.tenantRole : null; }
  get canRead() { return this.support || ['Owner','Manager','Staff'].includes(this.role || ''); }
  get canEdit() { return this.support || ['Owner','Manager'].includes(this.role || ''); }
  get dirty() {
    const d = this.active();
    if (!d) return false;
    try { return JSON.stringify(this.document()) !== JSON.stringify(d.document) || !this.theme && this.title !== d.title; }
    catch { return true; }
  }
  constructor() { if (typeof window !== 'undefined' && this.canRead && !this.support) void this.reload(); }
  private base() { return `/commerce/${this.theme ? 'theme' : 'storefront'}/tenants/${encodeURIComponent(this.tenant)}/${this.theme ? 'themes' : 'pages'}`; }
  private request<T>(method: 'GET' | 'PUT' | 'POST', path: string, body?: unknown) {
    return firstValueFrom(this.http.request<T>(method, this.base() + path, { body, headers: this.support ? { 'X-Support-Reason': this.reason.trim() } : {} }).pipe(timeout(10000)));
  }
  async signIn() { try { await this.session.signIn(this.tenant, this.theme ? 'theme' : 'storefront'); } catch { this.error.set('Sign-in could not be started.'); } }
  private async run(work: () => Promise<void>) {
    if (!this.canRead || this.busy() || this.support && !this.reason.trim()) return;
    this.busy.set(true); this.error.set(''); this.message.set('');
    try { await work(); }
    catch (e) {
      const title = e instanceof HttpErrorResponse ? e.error?.title : '';
      this.error.set(e instanceof SyntaxError ? 'The layout JSON is not valid.' : e instanceof HttpErrorResponse && e.status === 409
        ? 'Another change was saved first. Reload the draft and try again.' : title === 'InsufficientThemeContrast'
        ? 'Some color pairs need more contrast. Adjust the background or text color and preview again.' : 'The presentation request failed. Check the draft values and your access.');
    } finally { this.busy.set(false); }
  }
  private currentPath(id: string) { return this.theme ? '/publication' : '/' + id + '/publication'; }
  private async open(draft: Draft) {
    this.selected = draft.id; this.active.set(draft); this.title = draft.title || ''; this.advanced = false;
    this.json = JSON.stringify(draft.document, null, 2);
    this.themeDocument = this.theme ? structuredClone(draft.document) as ThemeDocument : null;
    this.layout = this.theme ? null : structuredClone(draft.document) as Layout;
    this.previewTheme.set(null); this.previewLayout.set(null); this.publication.set(null); this.versions.set([]);
    const [versions, publication] = await Promise.all([this.request<Version[]>('GET', '/' + draft.id + '/versions'), this.request<Publication | null>('GET', this.currentPath(draft.id))]);
    this.versions.set(versions); this.publication.set(publication);
  }
  select() { return this.run(async () => { const draft = this.drafts().find(d => d.id === this.selected); if (draft) await this.open(draft); }); }
  reload() { return this.run(async () => {
    const drafts = await this.request<Draft[]>('GET', ''); this.drafts.set(drafts);
    const selected = drafts.find(d => d.id === this.selected) || drafts[0]; if (selected) await this.open(selected); else this.active.set(null);
  }); }
  create() { return this.run(async () => {
    if (!this.canEdit || !this.newName.trim()) return;
    const row = await this.request<Draft>('POST', '', this.theme ? { name: this.newName.trim(), preset: this.preset } : { slug: this.slug.trim(), title: this.newName.trim(), template: this.preset });
    this.drafts.update(rows => [...rows, row]); await this.open(row); this.newName = ''; this.message.set('Draft created.');
  }); }
  addSection() {
    if (!this.layout || this.layout.sections.length >= 30) return;
    const used = new Set(this.layout.sections.flatMap(s => [s.desktop.order, s.mobile.order]));
    const order = Array.from({ length: 100 }, (_, i) => i + 1).find(i => !used.has(i)) || 100;
    this.layout.sections.push({ id: crypto.randomUUID(), kind: 'text', title: 'New section', text: null, references: [],
      desktop: { visible: true, order, columns: 1 }, mobile: { visible: true, order, columns: 1 } });
  }
  setAdvanced() { this.json = JSON.stringify(this.layout, null, 2); this.advanced = true; }
  private document() { return this.theme ? this.themeDocument : this.advanced ? JSON.parse(this.json) as Layout : this.layout; }
  preview() { return this.run(async () => {
    if (!this.canEdit) return;
    if (this.theme) {
      const view = await this.request<{ variables: Record<string, string> }>('POST', `/preview?mode=${this.colorMode}&mobile=${this.device === 'mobile'}`, this.document());
      this.previewTheme.set(safeThemeVariables(view.variables));
    } else this.previewLayout.set(await this.request<Layout>('POST', '/preview', this.document()));
  }); }
  save() { return this.run(async () => {
    const d = this.active(); if (!d || !this.canEdit) return;
    const row = await this.request<Draft>('PUT', '/' + d.id + '/draft', { version: d.version, document: this.document(), ...(!this.theme ? { title: this.title.trim() } : {}) });
    this.drafts.update(rows => rows.map(x => x.id === d.id ? row : x)); await this.open(row); this.message.set('Draft saved.');
  }); }
  publish() { return this.run(async () => {
    const d = this.active(); if (!d || !this.canEdit || this.dirty) return;
    const result = await this.request<Publication>('POST', '/' + d.id + '/publish', { version: d.version, publicationVersion: this.publication()?.publicationVersion || null });
    const row = { ...d, version: (this.theme ? result.themeVersion : result.pageVersion) || d.version };
    this.drafts.update(rows => rows.map(x => x.id === d.id ? row : x)); await this.open(row); this.message.set('Saved draft published to the store.');
  }); }
  rollback(version: Version) { return this.run(async () => {
    const d = this.active(), p = this.publication();
    if (!d || !p || !this.canEdit || this.dirty) return;
    await this.request<Publication>('POST', this.theme ? '/rollback' : '/' + d.id + '/rollback',
      { publicationVersion: p.publicationVersion, ...(this.theme ? { themeVersionId: version.id } : { layoutVersionId: version.id }) });
    this.publication.set(await this.request<Publication>('GET', this.currentPath(d.id))); this.message.set('Published configuration restored.');
  }); }
}
