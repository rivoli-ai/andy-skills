import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { APP_CONFIG, registryApiPrefix } from './config.service';
import {
  AuthConfigResponse,
  AuthProviderConfig,
} from '../auth/oidc-config.loader';
import { Observable, firstValueFrom, tap } from 'rxjs';

export interface RegistryAuthUser {
  id: string;
  email: string;
  name?: string | null;
  /** Optional parity with DevPilot UI when linking GitHub later. */
  githubUsername?: string | null;
}

export interface RegistryAuthResponse {
  token: string;
  user: RegistryAuthUser;
}

const TOKEN_KEY = 'auth_token';
const USER_KEY = 'auth_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly appConfig = inject(APP_CONFIG);

  private readonly tokenSignal = signal<string | null>(null);
  private readonly userSignal = signal<RegistryAuthUser | null>(null);
  private readonly isAuthenticated = signal(false);

  private readonly providerConfigs = signal<AuthProviderConfig[]>([]);
  private configLoaded = false;

  readonly token = this.tokenSignal.asReadonly();
  readonly user = this.userSignal.asReadonly();
  readonly authenticated = this.isAuthenticated.asReadonly();
  readonly frontendOidcProviders = computed(() =>
    this.providerConfigs().filter((p) => p.type === 'FrontendOidc'),
  );

  constructor() {
    const savedToken = localStorage.getItem(TOKEN_KEY);
    const savedUser = localStorage.getItem(USER_KEY);
    if (savedToken) {
      if (this.isJwtExpired(savedToken)) {
        localStorage.removeItem(TOKEN_KEY);
        localStorage.removeItem(USER_KEY);
      } else {
        this.tokenSignal.set(savedToken);
        this.isAuthenticated.set(true);
      }
    }
    if (savedUser && this.tokenSignal() !== null) {
      try {
        this.userSignal.set(JSON.parse(savedUser));
      } catch {
        /* ignore */
      }
    }
  }

  private isJwtExpired(token: string): boolean {
    try {
      const parts = token.split('.');
      if (parts.length < 2) return true;
      const payload = JSON.parse(atob(parts[1]));
      const exp: unknown = payload?.exp;
      if (typeof exp !== 'number') return false;
      return exp <= Math.floor(Date.now() / 1000) - 5;
    } catch {
      return true;
    }
  }

  async loadProviderConfig(): Promise<AuthProviderConfig[]> {
    if (this.configLoaded) return this.providerConfigs();
    const base = registryApiPrefix(this.appConfig);
    try {
      const response = await firstValueFrom(this.http.get<AuthConfigResponse>(`${base}/auth/config`));
      this.providerConfigs.set(response.providers ?? []);
      this.configLoaded = true;
      return response.providers ?? [];
    } catch (err) {
      console.error('Failed to load auth provider config', err);
      return [];
    }
  }

  getProviderConfig(name: string): AuthProviderConfig | undefined {
    return this.providerConfigs().find((p) => p.name.toLowerCase() === name.toLowerCase());
  }

  handleOidcTokenLogin(provider: string, idToken: string, accessToken?: string): Observable<RegistryAuthResponse> {
    const base = registryApiPrefix(this.appConfig);
    return this.http
      .post<RegistryAuthResponse>(`${base}/auth/${encodeURIComponent(provider)}/token`, {
        idToken,
        accessToken,
      })
      .pipe(tap((response) => this.setAuthState(response)));
  }

  getToken(): string | null {
    return this.tokenSignal();
  }

  getCurrentUser(): RegistryAuthUser | null {
    return this.userSignal();
  }

  isLoggedIn(): boolean {
    return this.isAuthenticated() && this.tokenSignal() !== null;
  }

  logout(): void {
    this.tokenSignal.set(null);
    this.userSignal.set(null);
    this.isAuthenticated.set(false);
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  }

  private setAuthState(response: RegistryAuthResponse): void {
    this.tokenSignal.set(response.token);
    this.userSignal.set(response.user);
    this.isAuthenticated.set(true);
    localStorage.setItem(TOKEN_KEY, response.token);
    localStorage.setItem(USER_KEY, JSON.stringify(response.user));
  }
}
