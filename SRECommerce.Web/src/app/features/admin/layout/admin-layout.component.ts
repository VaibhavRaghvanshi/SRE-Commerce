import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './admin-layout.component.html'
})
export class AdminLayoutComponent {
  auth = inject(AuthService);

  logout() {
    this.auth.logout();
    location.href = '/';
  }
}
