import { Component } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { HeaderComponent } from './shared/header/header.component';
import { ShopChatComponent } from './shared/shop-chat/shop-chat.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, HeaderComponent, ShopChatComponent],
  templateUrl: './app.component.html'
})
export class AppComponent {
  get isCommerceRoute() { return /^\/(stores|workspace|commerce|businesses|platform)(\/|\?|$)/.test(this.router.url); }
  get isAdminRoute() { return this.isCommerceRoute || /^\/admin(\/|\?|$)/.test(this.router.url); }
  constructor(private router: Router) {}
}
