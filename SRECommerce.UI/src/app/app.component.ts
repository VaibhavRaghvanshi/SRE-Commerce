import { Component, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { CommonModule, DOCUMENT } from '@angular/common';
import { HeaderComponent } from './shared/header/header.component';
import { ShopChatComponent } from './shared/shop-chat/shop-chat.component';
import { WorkspaceNavComponent } from './features/commerce/workspace/workspace-nav.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, HeaderComponent, ShopChatComponent, WorkspaceNavComponent],
  host: { '[class.commerce-app]': 'isCommerceRoute', '[class.commerce-workspace]': '!!workspaceTenant' },
  templateUrl: './app.component.html'
})
export class AppComponent {
  private readonly document = inject(DOCUMENT);
  focusContent(event: Event) { event.preventDefault(); const content = this.document.getElementById('commerce-content'); content?.focus({preventScroll:true}); content?.scrollIntoView({block:'start'}); }
  get isCommerceRoute() { return this.router.url.split(/[?#]/)[0] === '/' || /^\/(stores|workspace|commerce|businesses|platform)(\/|\?|$)/.test(this.router.url); }
  get workspaceTenant() { return /^\/workspace\/([a-f0-9-]{36})(?:\/|$)/i.exec(this.router.url)?.[1] || ''; }
  get isAdminRoute() { return this.isCommerceRoute || /^\/admin(\/|\?|$)/.test(this.router.url); }
  constructor(private router: Router) {}
}
