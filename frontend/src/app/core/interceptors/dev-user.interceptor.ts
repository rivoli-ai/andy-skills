import { HttpInterceptorFn } from '@angular/common/http';

const STORAGE_KEY = 'skill-registry-dev-user-id';
const AUTH_TOKEN_KEY = 'auth_token';

/** Sends optional `X-Dev-User-Id` from localStorage for audit trails (matches backend dev header). */
export const devUserInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.includes('/api')) {
    return next(req);
  }
  if (req.headers.has('Authorization') || localStorage.getItem(AUTH_TOKEN_KEY)?.trim()) {
    return next(req);
  }
  const id = localStorage.getItem(STORAGE_KEY)?.trim();
  if (!id) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { 'X-Dev-User-Id': id } }));
};

export const DEV_USER_STORAGE_KEY = STORAGE_KEY;
