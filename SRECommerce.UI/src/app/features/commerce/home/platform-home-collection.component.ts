import { CommonModule } from '@angular/common';
import { Component, Input, Output, EventEmitter } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommerceProduct, CommerceTenant } from '../../../core/models/commerce.models';
import { CommerceImageDirective } from '../../../core/directives/commerce-image.directive';

@Component({ selector: 'app-home-collection', standalone: true, imports: [CommonModule, RouterLink, CommerceImageDirective], templateUrl: './platform-home-collection.component.html', styleUrl: './platform-home-collection.component.scss' })
export class PlatformHomeCollectionComponent {
  @Input() tenant: CommerceTenant | null = null; @Input() products: CommerceProduct[] = []; @Input() store = ''; @Input() failedImages: string[] = [];
  @Input() loading = false; @Input() error = ''; @Input() total = 0;
  @Output() imageFailed = new EventEmitter<string>(); @Output() retry = new EventEmitter<void>();
}
