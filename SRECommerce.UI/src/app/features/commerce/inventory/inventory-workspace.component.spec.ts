import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { InventoryWorkspaceComponent } from './inventory-workspace.component';
import { MerchantInventoryService } from '../../../core/services/merchant-inventory.service';
import { MerchantCatalogService } from '../../../core/services/merchant-catalog.service';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
describe('Inventory operation retry identity',()=>{
  let post:jasmine.Spy;
  beforeEach(()=>{post=jasmine.createSpy('post').and.returnValue(throwError(()=>({status:503})));TestBed.configureTestingModule({providers:[provideZonelessChangeDetection(),{provide:ActivatedRoute,useValue:{snapshot:{paramMap:convertToParamMap({tenant:'tenant-a'})}}},{provide:CommerceSessionService,useValue:{token:'owner',access:()=>({selectedTenantId:'tenant-a',tenantRole:'Owner',isPlatformAdmin:false})}},{provide:MerchantCatalogService,useValue:{}},{provide:MerchantInventoryService,useValue:{post,lookups:()=>of({branches:[],warehouses:[],items:[],suppliers:[]}),balances:()=>of([]),purchases:()=>of([])}}]});});
  it('reuses the receipt operation ID after an uncertain response',()=>{const c=TestBed.createComponent(InventoryWorkspaceComponent).componentInstance;c.warehouseId='warehouse';c.stockVariant='variant';c.quantity=5;c.reason='Opening stock';c.receive();const first=post.calls.mostRecent().args[2];c.receive();const second=post.calls.mostRecent().args[2];expect(first.operationId).toBe(second.operationId);expect(second.quantity).toBe(5);expect(c.busy()).toBeFalse();});
  it('reuses both reservation ID and expiry when retrying the same hold',()=>{const c=TestBed.createComponent(InventoryWorkspaceComponent).componentInstance;c.warehouseId='warehouse';c.stockVariant='variant';c.reference='order-1';c.hold();const first=post.calls.mostRecent().args[2];c.hold();const second=post.calls.mostRecent().args[2];expect(second.id).toBe(first.id);expect(second.expiresAt).toBe(first.expiresAt);expect(second.items).toEqual(first.items);});
});
