import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { Category } from '../../core/models/catalog.models';
import { CategoryService } from '../../core/services/category.service';
import { AuthService } from '../../core/services/auth.service';
import { CartService } from '../../core/services/cart.service';

@Component({
  selector: 'app-header',
  standalone: true,
  templateUrl: './header.component.html',
  imports: [
    CommonModule,
    FormsModule,
    RouterLink
  ]
})
export class HeaderComponent implements OnInit {
  private categoryService = inject(CategoryService);
  auth = inject(AuthService);
  cart = inject(CartService);
  private router = inject(Router);

  categories: Category[] = [];
  q = '';

  private readonly demoCategories: Category[] = [
    {
      id: 1,
      name: 'Mobiles',
      slug: 'mobiles',
      imageUrl: '/assets/Images/categories/mobiles.jpg'
    },
    {
      id: 2,
      name: 'Laptops',
      slug: 'laptops',
      imageUrl: '/assets/Images/categories/laptops.jpg'
    },
    {
      id: 3,
      name: 'Fashion',
      slug: 'fashion',
      imageUrl: '/assets/Images/categories/fashion.jpg'
    },
    {
      id: 4,
      name: 'Beauty',
      slug: 'beauty',
      imageUrl: '/assets/Images/categories/beauty.jpg'
    },
    {
      id: 5,
      name: 'Home Decor',
      slug: 'home-decor',
      imageUrl: '/assets/Images/categories/home-decor.jpg'
    }
  ];

  // dropdown open/close flag
  isCategoriesOpen = false;

  ngOnInit(): void {
    this.categoryService.getAll().subscribe({
      next: cats => {
        this.categories = (cats && cats.length) ? cats : this.demoCategories;
      },
      error: () => {
        this.categories = this.demoCategories;
      }
    });
  }

  // sirf top-level categories (parentId == null)
  get topCategories(): Category[] {
    return this.categories.filter(c => !c.parentId);
  }

  toggleCategories() {
    this.isCategoriesOpen = !this.isCategoriesOpen;
  }

  go() {
    if (!this.q?.trim()) return;
    this.router.navigate(['/products'], { queryParams: { q: this.q.trim() } });
  }

  logout() {
    this.auth.logout();
    this.router.navigate(['/']);
  }

  goToCategory(cat: Category) {
    this.isCategoriesOpen = false;
    this.router.navigate(['/c', cat.slug]);
  }
}
