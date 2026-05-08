import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/** Requires registry JWT; anonymous users go to `/login` with `returnUrl`. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.isLoggedIn()) {
    return true;
  }
  void router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
  return false;
};
