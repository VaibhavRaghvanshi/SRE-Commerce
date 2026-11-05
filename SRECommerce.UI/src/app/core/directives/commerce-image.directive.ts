import { HttpClient } from '@angular/common/http';
import { Directive, ElementRef, effect, inject, input, output } from '@angular/core';
import { CommerceSessionService } from '../services/commerce-session.service';

@Directive({ selector: 'img[commerceImage]', standalone: true })
export class CommerceImageDirective {
  readonly commerceImage = input.required<string>();
  readonly imageTenant = input.required<string>();
  readonly commerceImageError = output<void>();
  private readonly element = inject<ElementRef<HTMLImageElement>>(ElementRef);
  private readonly http = inject(HttpClient);
  private readonly session = inject(CommerceSessionService);

  constructor() {
    effect(onCleanup => {
      const source = this.commerceImage(), tenant = this.imageTenant(), image = this.element.nativeElement;
      const managed = /^\/commerce\/catalog\/public\/tenants\/([a-f0-9-]{36})\/catalog\/media\/([a-f0-9-]{36})$/.exec(source);
      const member = !!this.session.token && this.session.access()?.selectedTenantId === tenant;
      image.removeAttribute('src');
      if (!managed || managed[1] !== tenant || !member || typeof window === 'undefined') {
        image.src = source; return;
      }
      let objectUrl: string | null = null;
      const subscription = this.http.get('/commerce/catalog/tenants/' + tenant + '/catalog/buyer/media/' + managed[2], { responseType: 'blob' })
        .subscribe({ next: blob => {
          if (!['image/png', 'image/jpeg'].includes(blob.type)) { this.commerceImageError.emit(); return; }
          objectUrl = URL.createObjectURL(blob); image.src = objectUrl;
        }, error: () => { image.removeAttribute('src'); this.commerceImageError.emit(); } });
      onCleanup(() => { subscription.unsubscribe(); image.removeAttribute('src'); if (objectUrl) URL.revokeObjectURL(objectUrl); });
    });
  }
}
