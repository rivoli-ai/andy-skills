import { HttpClient } from '@angular/common/http';
import { catchError, map, Observable, of } from 'rxjs';
import { LogLevel, OpenIdConfiguration } from 'angular-auth-oidc-client';
import { registryApiPrefix, type AppConfig } from '../services/config.service';

/** Response from GET /api/auth/config */
export interface AuthConfigResponse {
  providers: AuthProviderConfig[];
}

export interface AuthProviderConfig {
  name: string;
  type: 'Local' | 'BackendOAuth' | 'FrontendOidc';
  clientId?: string;
  authority?: string;
  scopes?: string;
  tenantId?: string;
  redirectUri?: string;
  authorizationUrl?: string;
}

export function loadOidcConfigs(http: HttpClient, config: AppConfig): Observable<OpenIdConfiguration[]> {
  const base = registryApiPrefix(config);
  return http.get<AuthConfigResponse>(`${base}/auth/config`).pipe(
    map((response) => {
      const oidcProviders = response.providers.filter((p) => p.type === 'FrontendOidc');
      return oidcProviders.map(
        (p): OpenIdConfiguration => ({
          configId: p.name,
          authority: p.authority ?? '',
          clientId: p.clientId ?? '',
          redirectUrl:
            typeof window !== 'undefined'
              ? `${window.location.origin}/auth/callback/${encodeURIComponent(p.name)}`
              : '',
          scope: p.scopes ?? 'openid profile email',
          responseType: 'code',
          postLogoutRedirectUri: typeof window !== 'undefined' ? window.location.origin : '',
          silentRenew: false,
          useRefreshToken: false,
          ignoreNonceAfterRefresh: true,
          triggerAuthorizationResultEvent: true,
          autoUserInfo: false,
          disableIdTokenValidation: true,
          logLevel: LogLevel.Warn,
        }),
      );
    }),
    catchError((err) => {
      console.warn('Failed to load auth config; OIDC providers unavailable:', err);
      return of([]);
    }),
  );
}
