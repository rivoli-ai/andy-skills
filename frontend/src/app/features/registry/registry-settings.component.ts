import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DEV_USER_STORAGE_KEY } from '../../core/interceptors/dev-user.interceptor';

@Component({
  standalone: true,
  selector: 'app-registry-settings',
  imports: [FormsModule],
  template: `
    <div class="settings-stack">
      <header class="settings-head">
        <span class="settings-eyebrow">Configuration</span>
        <h1 class="settings-title">Settings</h1>
        <p class="settings-lede">
          Runtime settings load from <code class="settings-code">/assets/config/config.json</code> at startup (API base URL and CLI hints).
          For local dev with <code class="settings-code">ng serve</code>, <code class="settings-code">proxy.conf.json</code> forwards
          <code class="settings-code">/api</code> to your Skill Registry backend.
        </p>
      </header>

      <section class="settings-section">
        <h2 class="settings-h2">Development</h2>
        <p class="settings-muted">
          Optional value sent as <code class="settings-code">X-Dev-User-Id</code> when you are not signed in with OIDC (registry JWT takes precedence when present).
        </p>
        <label class="settings-field">
          <span class="settings-label">Dev user ID</span>
          <input
            type="text"
            class="settings-input"
            name="devUserId"
            [(ngModel)]="devUserModel"
            (blur)="persistDevUser()"
            autocomplete="off"
            spellcheck="false"
            placeholder="e.g. local-dev-user"
          />
        </label>
      </section>
    </div>
  `,
  styles: `
    :host {
      display: block;
      flex: 1 1 auto;
      min-height: 0;
      overflow-x: hidden;
      overflow-y: auto;
      -webkit-overflow-scrolling: touch;
      box-sizing: border-box;
    }

    .settings-stack {
      max-width: 42rem;
      display: flex;
      flex-direction: column;
      gap: var(--space-10);
    }

    .settings-head {
      display: flex;
      flex-direction: column;
      gap: var(--space-3);
    }

    .settings-eyebrow {
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: var(--text-muted);
    }

    .settings-title {
      margin: 0;
      font-size: var(--text-2xl);
      font-weight: 700;
      letter-spacing: var(--tracking-tight);
      color: var(--text-primary);
    }

    .settings-lede {
      margin: 0;
      font-size: var(--text-base);
      line-height: var(--leading-relaxed);
      color: var(--text-secondary);
    }

    .settings-code {
      font-family: var(--font-mono);
      font-size: 0.9em;
      padding: 2px 6px;
      border-radius: var(--radius-sm);
      background: var(--surface-hover);
      border: 1px solid var(--border-light);
    }

    .settings-section {
      padding: var(--space-6);
      border-radius: var(--radius-xl);
      border: 1px solid var(--border-light);
      background: var(--surface-card);
      box-shadow: var(--shadow-card);
    }

    .settings-h2 {
      margin: 0 0 var(--space-2);
      font-size: var(--text-base);
      font-weight: 700;
      color: var(--text-primary);
    }

    .settings-muted {
      margin: 0 0 var(--space-5);
      font-size: var(--text-sm);
      line-height: var(--leading-relaxed);
      color: var(--text-tertiary);
    }

    .settings-field {
      display: flex;
      flex-direction: column;
      gap: var(--space-2);
    }

    .settings-label {
      font-size: var(--text-xs);
      font-weight: 600;
      color: var(--text-secondary);
    }

    .settings-input {
      max-width: 22rem;
      padding: var(--space-3) var(--space-4);
      border-radius: var(--radius-md);
      border: 1px solid var(--border-default);
      background: var(--surface-ground);
      color: var(--text-primary);
      font-family: var(--font-mono);
      font-size: var(--text-sm);
    }

    .settings-input:focus-visible {
      outline: none;
      border-color: var(--brand-primary);
      box-shadow: 0 0 0 3px color-mix(in srgb, var(--brand-primary) 22%, transparent);
    }
  `,
})
export class RegistrySettingsComponent implements OnInit {
  devUserModel = '';

  ngOnInit(): void {
    this.devUserModel = localStorage.getItem(DEV_USER_STORAGE_KEY) ?? '';
  }

  persistDevUser(): void {
    const v = this.devUserModel.trim();
    if (v) {
      localStorage.setItem(DEV_USER_STORAGE_KEY, v);
    } else {
      localStorage.removeItem(DEV_USER_STORAGE_KEY);
    }
  }
}
