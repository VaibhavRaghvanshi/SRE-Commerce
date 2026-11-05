import { CommonModule } from '@angular/common';
import { Component, Input, Output, EventEmitter } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommerceProduct, CommerceTenant } from '../../../core/models/commerce.models';
import { CommerceImageDirective } from '../../../core/directives/commerce-image.directive';

@Component({ selector: 'app-home-hero', standalone: true, imports: [CommonModule, RouterLink, CommerceImageDirective], templateUrl: './platform-home-hero.component.html', styleUrl: './platform-home-hero.component.scss' })
export class PlatformHomeHeroComponent {
  @Input() tenant: CommerceTenant | null = null; @Input() products: CommerceProduct[] = []; @Input() store = ''; @Input() failedImages: string[] = [];
  @Output() imageFailed = new EventEmitter<string>();
}
