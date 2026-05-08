import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { HttpClient, provideHttpClient, withInterceptorsFromDi, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideAuth, StsConfigHttpLoader, StsConfigLoader } from 'angular-auth-oidc-client';

import { routes } from './app.routes';
import { devUserInterceptor } from './core/interceptors/dev-user.interceptor';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { loadOidcConfigs } from './core/auth/oidc-config.loader';
import { APP_CONFIG, AppConfig } from './core/services/config.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(
      withInterceptorsFromDi(),
      withInterceptors([devUserInterceptor, authInterceptor]),
    ),
    provideAuth({
      loader: {
        provide: StsConfigLoader,
        useFactory: (http: HttpClient, config: AppConfig) =>
          new StsConfigHttpLoader(loadOidcConfigs(http, config)),
        deps: [HttpClient, APP_CONFIG],
      },
    }),
  ],
};
