import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { LoginRequest } from '../../../core/models/auth.models';

@Component({
  standalone: true,
  selector: 'app-login',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './login.component.html'
})
export class LoginComponent {
  auth = inject(AuthService);
  router = inject(Router);

  model: LoginRequest = {
    email: '',
    password: ''
  };

  loading = false;
  error: string | null = null;

  submit(form: NgForm) {
    if (form.invalid) return;

    this.loading = true;
    this.error = null;

    this.auth.login(this.model).subscribe({
      next: () => {
        this.loading = false;
        // login ke baad home pe le jao (baad me redirect better kar sakte)
        this.router.navigateByUrl('/');
      },
      error: err => {
        this.loading = false;
        this.error = err?.error?.message || 'Login failed';
      }
    });
  }
}
