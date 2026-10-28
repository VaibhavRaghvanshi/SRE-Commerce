import { Component, provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AppComponent } from './app.component';
import { HeaderComponent } from './shared/header/header.component';
import { ShopChatComponent } from './shared/shop-chat/shop-chat.component';

@Component({selector: 'app-header', standalone: true, template: 'Store header'})
class HeaderStub {}
@Component({selector: 'app-shop-chat', standalone: true, template: ''})
class ChatStub {}
@Component({standalone: true, template: ''})
class PageStub {}

describe('AppComponent routing shell', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideZonelessChangeDetection(), provideRouter([{path: '', component: PageStub}, {path: 'admin', component: PageStub}])]
    }).overrideComponent(AppComponent, {
      remove: {imports: [HeaderComponent, ShopChatComponent]},
      add: {imports: [HeaderStub, ChatStub]}
    }).compileComponents();
  });

  it('shows the storefront header on public routes', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-header')).toBeTruthy();
  });

  it('hides the storefront header after navigating to admin', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    await TestBed.inject(Router).navigateByUrl('/admin');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-header')).toBeNull();
  });
});
