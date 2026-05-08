import { InjectionToken } from '@angular/core';

export interface AppConfig {
  /**
   * Full API base URL **including** `/api` (e.g. `http://localhost:5289/api`), or empty string to use
   * same-origin `/api` (Angular dev proxy).
   */
  apiUrl: string;
  /** Shown in CLI install hints (`andy-skill install --registry …`). */
  cliRegistryUrl?: string;
}

export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG');

export const DEFAULT_CONFIG: AppConfig = {
  apiUrl: '',
  cliRegistryUrl: 'http://localhost:5289',
};

/** Prefix for `/namespaces`, `/auth/config`, etc. */
export function registryApiPrefix(config: AppConfig): string {
  const u = (config.apiUrl ?? '').trim();
  if (!u) {
    return '/api';
  }
  return u.replace(/\/+$/, '');
}
