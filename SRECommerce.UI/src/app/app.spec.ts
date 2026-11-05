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
      providers: [provideZonelessChangeDetection(), provideRouter([{path: '', component: PageStub}, {path: 'legacy-home', component: PageStub}, {path: 'admin', component: PageStub}])]
    }).overrideComponent(AppComponent, {
      remove: {imports: [HeaderComponent, ShopChatComponent]},
      add: {imports: [HeaderStub, ChatStub]}
    }).compileComponents();
  });

  it('preserves the legacy storefront header on its public route', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    await TestBed.inject(Router).navigateByUrl('/legacy-home');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-header')).toBeTruthy();
  });

  it('uses the modern homepage shell without the legacy header and chat', () => {
    const fixture = TestBed.createComponent(AppComponent); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-header')).toBeNull(); expect(fixture.nativeElement.querySelector('app-shop-chat')).toBeNull();
    expect(fixture.nativeElement.classList.contains('commerce-app')).toBeTrue();
  });

  it('hides the storefront header after navigating to admin', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    await TestBed.inject(Router).navigateByUrl('/admin');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-header')).toBeNull();
  });
});
