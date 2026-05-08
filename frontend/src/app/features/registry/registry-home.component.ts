import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NamespaceDto, RegistryApiService, registryApiErrorMessage } from '../../core/services/registry-api.service';

@Component({
  selector: 'app-registry-home',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './registry-home.component.html',
  styleUrl: './registry-home.component.css',
})
export class RegistryHomeComponent implements OnInit {
  private readonly api = inject(RegistryApiService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly namespaces = signal<NamespaceDto[]>([]);

  readonly nsBusy = signal(false);
  readonly nsFormError = signal<string | null>(null);
  readonly nsFormOk = signal<string | null>(null);

  nsSlugModel = '';
  nsDisplayName = '';
  nsDescription = '';
  nsVisibility: 'Private' | 'OrgVisible' = 'Private';

  ngOnInit(): void {
    this.reloadNamespaces();
  }

  reloadNamespaces(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.listNamespaces().subscribe({
      next: (rows) => {
        this.namespaces.set(rows);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(
          'Could not reach the API. Start Postgres (DevPilot) + run the Skill Registry API on port 5289.',
        );
        this.loading.set(false);
      },
    });
  }

  submitNamespace(): void {
    this.nsFormError.set(null);
    this.nsFormOk.set(null);
    const slug = this.nsSlugModel.trim();
    const displayName = this.nsDisplayName.trim();
    if (!slug || !displayName) {
      this.nsFormError.set('Slug and display name are required.');
      return;
    }
    this.nsBusy.set(true);
    this.api
      .createNamespace({
        slug,
        displayName,
        description: this.nsDescription.trim() || null,
        visibility: this.nsVisibility,
      })
      .subscribe({
        next: () => {
          this.nsBusy.set(false);
          this.nsSlugModel = '';
          this.nsDisplayName = '';
          this.nsDescription = '';
          this.nsVisibility = 'Private';
          this.nsFormOk.set(`Namespace “${slug}” created.`);
          this.reloadNamespaces();
        },
        error: (err) => {
          this.nsBusy.set(false);
          this.nsFormError.set(registryApiErrorMessage(err));
        },
      });
  }
}
