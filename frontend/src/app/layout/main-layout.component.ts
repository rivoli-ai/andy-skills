import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { DEV_USER_STORAGE_KEY } from '../core/interceptors/dev-user.interceptor';
import { ThemeService } from '../core/services/theme.service';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.css',
})
export class MainLayoutComponent implements OnInit {
  readonly theme = inject(ThemeService);

  /** Sent as `X-Dev-User-Id` on API calls until OIDC exists. */
  readonly devUserId = signal('');

  ngOnInit(): void {
    this.devUserId.set(localStorage.getItem(DEV_USER_STORAGE_KEY) ?? '');
  }

  persistDevUser(): void {
    const v = this.devUserId().trim();
    if (v) {
      localStorage.setItem(DEV_USER_STORAGE_KEY, v);
    } else {
      localStorage.removeItem(DEV_USER_STORAGE_KEY);
    }
  }

  onDevUserInput(ev: Event): void {
    const el = ev.target as HTMLInputElement | null;
    this.devUserId.set(el?.value ?? '');
  }

  toggleTheme(): void {
    this.theme.toggle();
  }
}
