import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';import { FormsModule } from '@angular/forms';import { ActivatedRoute, RouterLink } from '@angular/router';import { firstValueFrom } from 'rxjs';
import { CommerceCartService } from '../../../core/services/commerce-cart.service';import { CommerceSessionService } from '../../../core/services/commerce-session.service';import { CommerceCatalogService } from '../../../core/services/commerce-catalog.service';import { CommerceTenant } from '../../../core/models/commerce.models';import { CommerceCartPreview, CommercePreparation } from '../../../core/models/commerce-cart.models';
import { StoreLanguageService } from '../../../core/services/store-language.service';
import { StoreLanguageComponent } from '../presentation/store-language.component';
import type { CartRecoveryCandidate, CartReminderPreference } from '../../../core/services/commerce-cart.service';
@Component({selector:'app-commerce-cart',standalone:true,imports:[CommonModule,FormsModule,RouterLink,StoreLanguageComponent],templateUrl:'./cart.component.html',styleUrl:'./cart.component.css'})
export class CommerceCartComponent {
 readonly locale=inject(StoreLanguageService);
 readonly recoveryCandidate=signal<CartRecoveryCandidate|null>(null);
 readonly recoveryMessage=signal('');
 readonly reminder=signal<CartReminderPreference|null>(null);
 readonly reminderBusy=signal(false);
 readonly reminderError=signal('');
 readonly session=inject(CommerceSessionService);private readonly carts=inject(CommerceCartService);private readonly catalog=inject(CommerceCatalogService);private readonly route=inject(ActivatedRoute);
 readonly tenant=signal<CommerceTenant|null>(null);readonly busy=signal(false);readonly error=signal('');readonly preview=signal<CommerceCartPreview|null>(null);readonly prepared=signal<CommercePreparation|null>(null);readonly cart=computed(()=>this.tenant()?this.carts.view(this.tenant()!.tenantId):null);coupon='';quantities:Record<string,number>={};
 constructor(){void this.initialize();}
 private async initialize(){this.busy.set(true);try{const tenant=await firstValueFrom(this.catalog.tenant(this.route.snapshot.paramMap.get('slug')||''));if(!tenant.modules.includes('cart'))throw new Error('Cart is not enabled.');this.tenant.set(tenant);await this.reloadCore();}catch{this.error.set('Your cart is temporarily unavailable. Please retry.');}finally{this.busy.set(false);}}
 private async reloadCore(){
  const tenant=this.tenant();if(!tenant)return;
  this.preview.set(null);this.recoveryCandidate.set(null);this.reminder.set(null);
  const cart=await this.carts.load(tenant.tenantId);
  this.quantities=Object.fromEntries(cart.items.map(i=>[i.variantId,i.quantity]));this.prepared.set(null);
  const recovery=this.carts.recovery(tenant.tenantId).catch(()=>null);
  const reminder=tenant.modules.includes('notifications') && !cart.guest
   ? this.carts.reminder(tenant.tenantId,cart.id).catch(()=>null) : Promise.resolve(null);
  try{this.preview.set(cart.items.length?await this.carts.preview(tenant.tenantId,this.coupon):null);}
  finally{this.recoveryCandidate.set(await recovery);this.reminder.set(await reminder);}
 }
 async setReminder(enabled: boolean) {
  const tenant = this.tenant(), cart = this.cart();
  if (!tenant || !cart || this.busy() || this.reminderBusy()) return;
  this.reminderBusy.set(true); this.reminderError.set('');
  try { this.reminder.set(await this.carts.reminder(tenant.tenantId, cart.id, enabled)); }
  catch { this.reminderError.set('We could not update your cart reminder. Please try again.'); }
  finally { this.reminderBusy.set(false); }
 }
 restoreCart(){
  const tenant=this.tenant(),candidate=this.recoveryCandidate();
  if(!tenant||!candidate)return;
  void this.work(async()=>{
   this.recoveryMessage.set('');
   await this.carts.recover(tenant.tenantId,candidate.id);
   this.recoveryMessage.set('Available items restored. Review your current price and stock.');
  });
 }
 private async work(action:()=>Promise<unknown>){if(this.busy())return;this.busy.set(true);this.error.set('');this.prepared.set(null);try{await action();await this.reloadCore();}catch{this.error.set('The change could not be confirmed. Retry the same action, or reload to check the saved cart. Price, coupon or stock may have changed.');}finally{this.busy.set(false);}}
 reload(){void this.work(()=>Promise.resolve());}
 update(variant:string){const quantity=Number(this.quantities[variant]);if(!(quantity>0&&quantity<=1000&&Math.abs(quantity*1000-Math.round(quantity*1000))<0.000001)){this.error.set('Enter a quantity from 0.001 to 1000, using at most three decimal places.');return;}void this.work(()=>this.carts.set(this.tenant()!.tenantId,variant,quantity));}
 remove(variant:string){void this.work(()=>this.carts.remove(this.tenant()!.tenantId,variant));}
 refreshPrice(){this.reload();}
 async signIn(){const tenant=this.tenant();if(tenant)try{await this.session.signInStore(tenant.tenantId,tenant.slug);}catch{this.error.set('Sign-in could not start. Please try again.');}}
 async signOut(){if(this.busy())return;this.recoveryCandidate.set(null);this.recoveryMessage.set('');this.reminder.set(null);await this.session.signOut();this.preview.set(null);this.reload();}
 async prepare(){if(this.busy()||!this.tenant())return;this.busy.set(true);this.error.set('');try{this.prepared.set(await this.carts.prepare(this.tenant()!.tenantId,this.coupon));}catch{this.error.set('Checkout preparation failed. Reload the cart to check current price and available stock.');}finally{this.busy.set(false);}}
}
