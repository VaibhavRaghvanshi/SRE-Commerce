import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { authInterceptor } from './auth.interceptor';

describe('API bearer token scope', () => {
  beforeEach(() => TestBed.configureTestingModule({providers: [
    provideZonelessChangeDetection(), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(),
    {provide: AuthService, useValue: {token: 'test-token'}}
  ]}));
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('attaches the token to API requests', () => {
    TestBed.inject(HttpClient).get('/api/auth/me').subscribe();
    const request = TestBed.inject(HttpTestingController).expectOne('/api/auth/me');
    expect(request.request.headers.get('Authorization')).toBe('Bearer test-token');
    request.flush({});
  });

  it('does not send the token to external origins', () => {
    TestBed.inject(HttpClient).get('https://external.example/api/data').subscribe();
    const request = TestBed.inject(HttpTestingController).expectOne('https://external.example/api/data');
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush({});
  });

  it('does not match API lookalike prefixes', () => {
    TestBed.inject(HttpClient).get('/api-other/data').subscribe();
    const request = TestBed.inject(HttpTestingController).expectOne('/api-other/data');
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush({});
  });
});
