import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CommerceSessionService } from '../../../core/services/commerce-session.service';
@Component({selector:'app-commerce-callback',standalone:true,template:'<main style="max-width:600px;margin:80px auto;padding:24px"><h1>Signing you in</h1><p role="status">{{message()}}</p><a href="/">Return home</a></main>'})
export class CommerceCallbackComponent {
  readonly message=signal('Checking your secure session…');private readonly session=inject(CommerceSessionService);private readonly router=inject(Router);
  constructor(){void this.session.callback().then(path=>this.router.navigateByUrl(path,{replaceUrl:true})).catch(()=>this.message.set('Sign-in could not be completed. Return to your business and try again.'));}
}
