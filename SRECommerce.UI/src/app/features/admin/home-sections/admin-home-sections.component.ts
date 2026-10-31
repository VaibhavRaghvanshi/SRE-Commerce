import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable, mapTo } from 'rxjs';
import { AdminHomeService } from '../../../core/services/admin-home.service';
import { ApiService } from '../../../core/services/api.service';
import {
  AdminHomeSection,
  UpsertHomeSectionRequest,
  HomeSectionItem,
  HomeSectionItemUpsert
} from '../../../core/models/admin-home.models';
import { Category, Product, PagedResult } from '../../../core/models/catalog.models';
import { Banner } from '../../../core/models/banner.models';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin-home-sections.component.html'
})
export class AdminHomeSectionsComponent {
  private svc = inject(AdminHomeService);
  private api = inject(ApiService);

  loading = signal(true);
  saving = signal(false);
  error = signal<string | null>(null);

  sections = signal<AdminHomeSection[]>([]);
  ordered = computed(() =>
    [...this.sections()].sort((a, b) => a.sortOrder - b.sortOrder)
  );

  items = signal<HomeSectionItem[]>([]);
  itemsLoading = signal(false);
  itemsSaving = signal(false);

  categories = signal<Category[]>([]);
  products = signal<Product[]>([]);
  banners = signal<Banner[]>([]);

  selectedEntityId = signal<number | null>(null);
  placeholder = '/assets/Images/categories/home-decor.jpg';

  editId = signal<number | null>(null);

  f_type = signal<number>(1);
  f_title = signal<string>('Shop by Category');
  f_enabled = signal<boolean>(true);
  f_itemsToShow = signal<number>(8);
  f_cardsPerRow = signal<number>(4);
  f_sourceKey = signal<string>('');
  f_styleJson = signal<string>('');

  get vm_type() { return this.f_type(); }
  set vm_type(v: number) { this.f_type.set(Number(v)); }

  get vm_title() { return this.f_title(); }
  set vm_title(v: string) { this.f_title.set(v ?? ''); }

  get vm_enabled() { return this.f_enabled(); }
  set vm_enabled(v: boolean) { this.f_enabled.set(!!v); }

  get vm_itemsToShow() { return this.f_itemsToShow(); }
  set vm_itemsToShow(v: number) { this.f_itemsToShow.set(Number(v)); }

  get vm_cardsPerRow() { return this.f_cardsPerRow(); }
  set vm_cardsPerRow(v: number) { this.f_cardsPerRow.set(Number(v)); }

  get vm_sourceKey() { return this.f_sourceKey(); }
  set vm_sourceKey(v: string) { this.f_sourceKey.set(v ?? ''); }

  get vm_styleJson() { return this.f_styleJson(); }
  set vm_styleJson(v: string) { this.f_styleJson.set(v ?? ''); }

  ngOnInit() {
    this.load();
    this.loadLookups();
  }

  load() {
    this.loading.set(true);
    this.error.set(null);

    this.svc.list().subscribe({
      next: (x: AdminHomeSection[]) => {
        this.sections.set(x || []);
        this.loading.set(false);
      },
      error: (e: any) => {
        this.error.set(e?.message || 'Failed to load');
        this.loading.set(false);
      }
    });
  }

  loadLookups() {
    this.api.get<Category[]>('/categories').subscribe(res => this.categories.set(res || []));
    this.api.get<PagedResult<Product>>('/products', { page: 1, pageSize: 1000 }).subscribe(res => {
      this.products.set(res?.items || []);
    });
    this.api.get<Banner[]>('/admin/banners').subscribe(res => this.banners.set(res || []));
  }

  resetForm() {
    this.editId.set(null);
    this.f_type.set(1);
    this.f_title.set('Shop by Category');
    this.f_enabled.set(true);
    this.f_itemsToShow.set(8);
    this.f_cardsPerRow.set(4);
    this.f_sourceKey.set('');
    this.f_styleJson.set('');
    this.items.set([]);
    this.selectedEntityId.set(null);
  }

  edit(s: AdminHomeSection) {
    this.editId.set(s.id);
    this.f_type.set(s.type);
    this.f_title.set(s.title || '');
    this.f_enabled.set(!!s.isEnabled);
    this.f_itemsToShow.set(s.itemsToShow || 8);
    this.f_cardsPerRow.set(s.cardsPerRow || 4);
    this.f_sourceKey.set(s.sourceKey || '');
    this.f_styleJson.set(s.styleJson || '');
    this.loadItems(s.id);
  }

  private buildPayload(sortOrder: number): UpsertHomeSectionRequest {
    return {
      type: this.f_type(),
      title: this.f_title(),
      isEnabled: this.f_enabled(),
      sortOrder,
      itemsToShow: this.f_itemsToShow(),
      cardsPerRow: this.f_cardsPerRow(),
      sourceKey: this.f_sourceKey() || null,
      styleJson: this.f_styleJson() || null
    };
  }

  save() {
    this.saving.set(true);
    this.error.set(null);

    const list = this.ordered();
    const editId = this.editId();

    const sortOrder = editId
      ? (list.find(x => x.id === editId)?.sortOrder ?? (list.length + 1))
      : (list.length + 1);

    const payload = this.buildPayload(sortOrder);

    const req$: Observable<void> = editId
      ? this.svc.update(editId, payload)
      : this.svc.create(payload).pipe(mapTo(void 0));

    req$.subscribe({
      next: () => {
        this.saving.set(false);
        this.resetForm();
        this.load();
      },
      error: (e: any) => {
        this.saving.set(false);
        this.error.set(e?.message || 'Save failed');
      }
    });
  }

  remove(s: AdminHomeSection) {
    if (!confirm(`Delete section "${s.title || s.id}"?`)) return;

    this.svc.delete(s.id).subscribe({
      next: () => this.load(),
      error: (e: any) => this.error.set(e?.message || 'Delete failed')
    });
  }

  toggle(s: AdminHomeSection) {
    const payload: UpsertHomeSectionRequest = {
      type: s.type,
      title: s.title,
      isEnabled: !s.isEnabled,
      sortOrder: s.sortOrder,
      itemsToShow: s.itemsToShow,
      cardsPerRow: s.cardsPerRow,
      sourceKey: s.sourceKey || null,
      styleJson: s.styleJson || null
    };

    this.svc.update(s.id, payload).subscribe({
      next: () => this.load(),
      error: (e: any) => this.error.set(e?.message || 'Update failed')
    });
  }

  moveUp(s: AdminHomeSection) {
    const list = this.ordered();
    const idx = list.findIndex(x => x.id === s.id);
    if (idx <= 0) return;
    this.swapAndSave(list[idx], list[idx - 1]);
  }

  moveDown(s: AdminHomeSection) {
    const list = this.ordered();
    const idx = list.findIndex(x => x.id === s.id);
    if (idx < 0 || idx >= list.length - 1) return;
    this.swapAndSave(list[idx], list[idx + 1]);
  }

  private swapAndSave(a: AdminHomeSection, b: AdminHomeSection) {
    const aNew = { ...a, sortOrder: b.sortOrder };
    const bNew = { ...b, sortOrder: a.sortOrder };

    this.svc.update(aNew.id, {
      type: aNew.type,
      title: aNew.title,
      isEnabled: aNew.isEnabled,
      sortOrder: aNew.sortOrder,
      itemsToShow: aNew.itemsToShow,
      cardsPerRow: aNew.cardsPerRow,
      sourceKey: aNew.sourceKey || null,
      styleJson: aNew.styleJson || null
    }).subscribe({
      next: () => {
        this.svc.update(bNew.id, {
          type: bNew.type,
          title: bNew.title,
          isEnabled: bNew.isEnabled,
          sortOrder: bNew.sortOrder,
          itemsToShow: bNew.itemsToShow,
          cardsPerRow: bNew.cardsPerRow,
          sourceKey: bNew.sourceKey || null,
          styleJson: bNew.styleJson || null
        }).subscribe({
          next: () => this.load(),
          error: (e: any) => this.error.set(e?.message || 'Reorder failed')
        });
      },
      error: (e: any) => this.error.set(e?.message || 'Reorder failed')
    });
  }

  typeLabel(t: number) {
    return t === 1 ? 'Categories Grid' : t === 2 ? 'Banner Slider' : 'Products Grid';
  }

  // --------- Items management ---------

  private expectedEntityType(sectionType: number): number {
    return sectionType === 1 ? 1 : sectionType === 2 ? 3 : 2;
  }

  loadItems(sectionId: number) {
    this.itemsLoading.set(true);
    this.svc.listItems(sectionId).subscribe({
      next: res => {
        this.items.set(res || []);
        this.itemsLoading.set(false);
      },
      error: () => {
        this.items.set([]);
        this.itemsLoading.set(false);
      }
    });
  }

  availableOptions() {
    const t = this.f_type();
    if (t === 1) return this.categories().map(c => ({ id: c.id, title: c.name, imageUrl: c.imageUrl }));
    if (t === 3) return this.products().map(p => ({ id: p.id, title: p.name, imageUrl: p.imageUrl }));
    return this.banners().map(b => ({ id: b.id, title: b.title, imageUrl: b.imageUrl }));
  }

  addItem() {
    const sectionId = this.editId();
    const entityId = this.selectedEntityId();
    if (!sectionId || !entityId) return;

    if (this.items().some(i => i.entityId === entityId && i.entityType === this.expectedEntityType(this.f_type()))) {
      return;
    }

    const nextOrder = this.items().length + 1;
    const newItem: HomeSectionItem = {
      id: 0,
      entityType: this.expectedEntityType(this.f_type()),
      entityId,
      sortOrder: nextOrder,
      isEnabled: true,
      title: this.availableOptions().find(x => x.id === entityId)?.title,
      imageUrl: this.availableOptions().find(x => x.id === entityId)?.imageUrl
    };

    const updated = [...this.items(), newItem];
    this.items.set(updated);
    this.selectedEntityId.set(null);
    this.saveItems();
  }

  removeItem(i: HomeSectionItem) {
    const updated = this.items().filter(x => x !== i);
    this.items.set(this.normalizeItems(updated));
    this.saveItems();
  }

  moveItemUp(i: HomeSectionItem) {
    const list = [...this.items()];
    const idx = list.indexOf(i);
    if (idx <= 0) return;
    [list[idx - 1], list[idx]] = [list[idx], list[idx - 1]];
    this.items.set(this.normalizeItems(list));
    this.saveItems();
  }

  moveItemDown(i: HomeSectionItem) {
    const list = [...this.items()];
    const idx = list.indexOf(i);
    if (idx < 0 || idx >= list.length - 1) return;
    [list[idx + 1], list[idx]] = [list[idx], list[idx + 1]];
    this.items.set(this.normalizeItems(list));
    this.saveItems();
  }

  toggleItem(i: HomeSectionItem) {
    const updated = this.items().map(x => x === i ? { ...x, isEnabled: !x.isEnabled } : x);
    this.items.set(updated);
    this.saveItems();
  }

  private normalizeItems(list: HomeSectionItem[]) {
    return list.map((x, idx) => ({ ...x, sortOrder: idx + 1 }));
  }

  private saveItems() {
    const sectionId = this.editId();
    if (!sectionId) return;

    const payload: HomeSectionItemUpsert[] = this.items().map((x, idx) => ({
      entityType: x.entityType,
      entityId: x.entityId,
      sortOrder: idx + 1,
      isEnabled: x.isEnabled
    }));

    this.itemsSaving.set(true);
    this.svc.saveItems(sectionId, payload).subscribe({
      next: () => {
        this.itemsSaving.set(false);
        this.loadItems(sectionId);
      },
      error: () => {
        this.itemsSaving.set(false);
      }
    });
  }

  itemTitle(i: HomeSectionItem) {
    return i.title || `Item #${i.entityId}`;
  }

  itemImage(i: HomeSectionItem) {
    return i.imageUrl || this.placeholder;
  }

  applyDesignPreset(preset: 'pink' | 'white' | 'sale' | 'compact') {
    const presets: Record<string, string> = {
      pink: '{"variant":"tiles","tone":"pink","bg":"#fff1f7","accent":"#e83e8c"}',
      white: '{"variant":"dense","tone":"white","bg":"#ffffff","accent":"#d63384"}',
      sale: '{"variant":"sale","tone":"pink","bg":"#ffe4ef","accent":"#be185d"}',
      compact: '{"variant":"compact","tone":"blush","bg":"#fff7fb","accent":"#db2777"}'
    };

    this.f_styleJson.set(presets[preset]);
  }
}
