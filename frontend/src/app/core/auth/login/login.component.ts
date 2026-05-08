import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { AuthService } from '../../services/auth.service';
import type { AuthProviderConfig } from '../oidc-config.loader';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css',
})
export class LoginComponent implements OnInit {
  readonly authService = inject(AuthService);
  private readonly oidcSecurityService = inject(OidcSecurityService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly configLoaded = signal(false);
  readonly sessionExpired = signal(false);
  private returnUrl: string | null = null;

  async ngOnInit(): Promise<void> {
    const params = this.route.snapshot.queryParamMap;
    const ret = params.get('returnUrl');
    this.returnUrl = ret && ret.startsWith('/') && !ret.startsWith('//') ? ret : null;
    if (params.get('sessionExpired') === '1') {
      this.sessionExpired.set(true);
    }

    if (this.authService.isLoggedIn()) {
      void this.router.navigateByUrl(this.returnUrl ?? '/');
      return;
    }

    await this.authService.loadProviderConfig();
    this.configLoaded.set(true);
    if (this.authService.frontendOidcProviders().length === 0 && !this.error()) {
      this.error.set(
        'No OIDC providers are enabled on the registry API. Enable AzureAd or Duende in appsettings (AuthProviders), or continue using dev sign-in when your deployment supports it.',
      );
    }
  }

  loginWithOidc(provider: AuthProviderConfig): void {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.oidcSecurityService.authorize(provider.name);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : String(err);
      this.error.set(msg || `Failed to start ${provider.name} sign-in`);
      this.loading.set(false);
    }
  }

  providerLabel(provider: AuthProviderConfig): string {
    const map: Record<string, string> = {
      azuread: 'Microsoft',
      duende: 'Duende',
    };
    return map[provider.name.toLowerCase()] ?? provider.name;
  }
}
