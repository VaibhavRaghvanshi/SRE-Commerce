import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CatalogTransferComponent } from './catalog-transfer.component';
import { CatalogEditor } from '../../../core/services/merchant-catalog.service';

describe('Catalog import uncertain-result recovery', () => {
  it('locks a lost-response batch and retries its original key and payload before announcing success', async () => {
    TestBed.configureTestingModule({providers:[provideZonelessChangeDetection(),provideHttpClient(),provideHttpClientTesting()]});
    const fixture=TestBed.createComponent(CatalogTransferComponent), component=fixture.componentInstance, http=TestBed.inject(HttpTestingController);
    component.tenant='own-tenant'; const created=spyOn(component.changed,'emit');
    component.products.set([{name:'Synthetic draft',slug:'synthetic-draft'} as CatalogEditor]);
    const first=component.apply(), original=http.expectOne('/commerce/catalog/tenants/own-tenant/catalog/import'), body=structuredClone(original.request.body);
    original.flush(null,{status:503,statusText:'Service Unavailable'});await first;
    expect(component.uncertain()).toBeTrue();component.clear();expect(component.products().length).toBe(1);expect(created).not.toHaveBeenCalled();
    const retry=component.apply(), repeated=http.expectOne('/commerce/catalog/tenants/own-tenant/catalog/import');expect(repeated.request.body).toEqual(body);
    repeated.flush(['11111111-1111-1111-1111-111111111111']);await retry;
    expect(component.uncertain()).toBeFalse();expect(component.products()).toEqual([]);expect(created).toHaveBeenCalledTimes(1);http.verify();fixture.destroy();
  });
});
