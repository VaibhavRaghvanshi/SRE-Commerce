import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { RegisterRequest } from '../../../core/models/auth.models';

@Component({
  standalone: true,
  selector: 'app-register',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './register.component.html'
})
export class RegisterComponent {
  auth = inject(AuthService);
  router = inject(Router);

  model: RegisterRequest = {
    fullName: '',
    email: '',
    password: ''
  };

  loading = false;
  error: string | null = null;

  submit(form: NgForm) {
    if (form.invalid) return;

    this.loading = true;
    this.error = null;

    this.auth.register(this.model).subscribe({
      next: () => {
        this.loading = false;
        this.router.navigateByUrl('/');
      },
      error: err => {
        this.loading = false;
        this.error = err?.error?.message || 'Registration failed';
      }
    });
  }
}
