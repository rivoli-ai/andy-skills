import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { AuthService } from '../../services/auth.service';
import { registryApiErrorMessage } from '../../services/registry-api.service';
import { take } from 'rxjs/operators';

@Component({
  selector: 'app-auth-callback',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="callback-page">
      <div class="callback-card">
        @if (loading()) {
          <div class="callback-spinner"></div>
          <h2>{{ statusMessage() }}</h2>
          <p class="callback-sub">Please wait…</p>
        } @else if (error()) {
          <h2>Sign-in failed</h2>
          <p class="callback-err">{{ error() }}</p>
          <button type="button" class="callback-btn" (click)="goToLogin()">Back to sign in</button>
        }
      </div>
    </div>
  `,
  styles: `
    .callback-page {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 2rem;
      background: var(--surface-ground);
    }
    .callback-card {
      max-width: 400px;
      width: 100%;
      padding: 2.5rem;
      border-radius: var(--radius-xl);
      border: 1px solid var(--border-light);
      background: var(--surface-card);
      box-shadow: var(--shadow-card);
      text-align: center;
    }
    h2 {
      margin: 0 0 0.5rem;
      font-size: 1.25rem;
      color: var(--text-primary);
    }
    .callback-sub {
      margin: 0;
      color: var(--text-secondary);
      font-size: 0.875rem;
    }
    .callback-spinner {
      width: 48px;
      height: 48px;
      margin: 0 auto 1rem;
      border: 4px solid var(--border-light);
      border-top-color: var(--brand-primary);
      border-radius: 50%;
      animation: cb-spin 0.9s linear infinite;
    }
    @keyframes cb-spin {
      to {
        transform: rotate(360deg);
      }
    }
    .callback-err {
      color: var(--error-600);
      font-size: 0.875rem;
      margin: 1rem 0;
    }
    .callback-btn {
      margin-top: 1rem;
      padding: 0.6rem 1.25rem;
      border-radius: var(--radius-md);
      border: none;
      background: var(--brand-primary);
      color: var(--text-inverse);
      font-weight: 600;
      cursor: pointer;
    }
  `,
})
export class CallbackComponent implements OnInit {
  loading = signal(true);
  error = signal<string | null>(null);
  statusMessage = signal('Completing sign-in…');

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly oidcSecurityService: OidcSecurityService,
    private readonly authService: AuthService,
  ) {}

  async ngOnInit(): Promise<void> {
    await this.authService.loadProviderConfig();

    const provider = this.route.snapshot.paramMap.get('provider') ?? '';
    const config = this.authService.getProviderConfig(provider);

    if (!config || config.type !== 'FrontendOidc') {
      this.error.set(config ? `Provider "${provider}" is not OIDC.` : `Unknown provider: ${provider}`);
      this.loading.set(false);
      return;
    }

    this.route.queryParams.pipe(take(1)).subscribe((params) => {
      const errorParam = params['error'];
      if (errorParam) {
        this.error.set(`${provider}: ${errorParam}`);
        this.loading.set(false);
        return;
      }

      const url = typeof window !== 'undefined' ? window.location.href : this.router.url;
      this.oidcSecurityService.checkAuth(url, provider).subscribe({
        next: (loginResponse) => {
          if (!loginResponse.isAuthenticated || (!loginResponse.accessToken && !loginResponse.idToken)) {
            this.error.set(loginResponse.errorMessage || `${provider} did not return tokens`);
            this.loading.set(false);
            return;
          }
          this.authService.handleOidcTokenLogin(provider, loginResponse.idToken, loginResponse.accessToken).subscribe({
            next: () => void this.router.navigateByUrl('/'),
            error: (err: unknown) => {
              this.error.set(registryApiErrorMessage(err));
              this.loading.set(false);
            },
          });
        },
        error: (err: unknown) => {
          const msg = err instanceof Error ? err.message : String(err);
          this.error.set(msg || `${provider} sign-in failed`);
          this.loading.set(false);
        },
      });
    });
  }

  goToLogin(): void {
    void this.router.navigate(['/login']);
  }
}
