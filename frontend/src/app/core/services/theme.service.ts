import { Injectable, signal } from '@angular/core';

export type ThemeMode = 'light' | 'dark';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly storageKey = 'skill-registry-theme';

  readonly theme = signal<ThemeMode>('light');

  constructor() {
    const saved = localStorage.getItem(this.storageKey) as ThemeMode | null;
    const prefersDark = window.matchMedia?.('(prefers-color-scheme: dark)')?.matches ?? false;
    this.apply(saved ?? (prefersDark ? 'dark' : 'light'));
  }

  toggle(): void {
    this.apply(this.theme() === 'light' ? 'dark' : 'light');
  }

  private apply(mode: ThemeMode): void {
    this.theme.set(mode);
    document.documentElement.dataset['theme'] = mode;
    localStorage.setItem(this.storageKey, mode);
  }
}
