import { Component } from '@angular/core';
import { Router, NavigationEnd, RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { filter } from 'rxjs/operators';
import { HeaderComponent } from './shared/header/header.component';
import { ShopChatComponent } from './shared/shop-chat/shop-chat.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, HeaderComponent, ShopChatComponent],
  templateUrl: './app.component.html'
})
export class AppComponent {
  isAdminRoute = false;
  get isCommerceRoute(){return /^\/(stores|workspace|commerce)(\/|$)/.test(this.router.url);}

  constructor(private router: Router) {
    this.router.events
      .pipe(filter(e => e instanceof NavigationEnd))
      .subscribe(() => {
        this.isAdminRoute = /^\/(admin|stores|workspace|commerce)(\/|$)/.test(this.router.url);
      });

    this.isAdminRoute = /^\/(admin|stores|workspace|commerce)(\/|$)/.test(this.router.url);
  }
}
