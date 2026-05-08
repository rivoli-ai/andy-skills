import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const token = authService.getToken();
  const usesAppJwt = !req.headers.has('Authorization');

  const outgoing =
    token && usesAppJwt ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(outgoing).pipe(
    catchError((err: unknown) => {
      if (
        err instanceof HttpErrorResponse &&
        err.status === 401 &&
        usesAppJwt &&
        shouldRedirectOnUnauthorized(req.url, authService)
      ) {
        authService.logout();
        const current = router.url || '/';
        if (!current.startsWith('/login')) {
          void router.navigate(['/login'], {
            queryParams: { returnUrl: current, sessionExpired: '1' },
          });
        }
      }
      return throwError(() => err);
    }),
  );
};

function shouldRedirectOnUnauthorized(url: string, authService: AuthService): boolean {
  if (!authService.isLoggedIn()) return false;
  if (/\/api\/auth\//i.test(url)) return false;
  return true;
}
