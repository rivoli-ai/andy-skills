import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription, combineLatest } from 'rxjs';
import { RegistryApiService, registryApiErrorMessage } from '../../core/services/registry-api.service';

@Component({
  selector: 'app-namespace-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './namespace-form.component.html',
  styleUrl: './namespace-form.component.css',
})
export class NamespaceFormComponent implements OnInit, OnDestroy {
  private readonly api = inject(RegistryApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private sub?: Subscription;

  readonly mode = signal<'create' | 'edit'>('create');
  readonly loadingNs = signal(false);
  readonly pageError = signal<string | null>(null);
  readonly saveBusy = signal(false);
  readonly saveError = signal<string | null>(null);

  /** Namespace slug when editing (route param). */
  editSlug = '';

  nsSlugModel = '';
  nsDisplayName = '';
  nsDescription = '';
  nsVisibility: 'Private' | 'OrgVisible' = 'Private';

  ngOnInit(): void {
    this.sub = combineLatest([this.route.data, this.route.paramMap]).subscribe(([data, pm]) => {
      const formMode = data['namespaceFormMode'] as 'create' | 'edit' | undefined;
      if (formMode === 'create') {
        this.mode.set('create');
        this.editSlug = '';
        this.resetCreateFields();
        this.pageError.set(null);
        this.saveError.set(null);
        this.loadingNs.set(false);
        return;
      }
      if (formMode === 'edit') {
        const slug = pm.get('slug');
        if (!slug) {
          return;
        }
        this.mode.set('edit');
        this.editSlug = slug;
        this.loadNamespace(slug);
      }
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  private resetCreateFields(): void {
    this.nsSlugModel = '';
    this.nsDisplayName = '';
    this.nsDescription = '';
    this.nsVisibility = 'Private';
  }

  private loadNamespace(slug: string): void {
    this.loadingNs.set(true);
    this.pageError.set(null);
    this.saveError.set(null);
    this.api.listNamespaces().subscribe({
      next: (rows) => {
        const ns = rows.find((n) => n.slug === slug);
        if (!ns) {
          this.pageError.set(
            `You don't have permission to manage namespace "${slug}", or it isn't visible to your account. Ask a namespace owner or admin for Admin access.`,
          );
          this.loadingNs.set(false);
          return;
        }
        this.nsSlugModel = ns.slug;
        this.nsDisplayName = ns.displayName;
        this.nsDescription = ns.description ?? '';
        this.nsVisibility = (ns.visibility === 'OrgVisible' ? 'OrgVisible' : 'Private') as 'Private' | 'OrgVisible';
        this.loadingNs.set(false);
      },
      error: () => {
        this.pageError.set(
          'Cannot reach the API. Run Postgres + the Skill Registry API (port 5289) and retry.',
        );
        this.loadingNs.set(false);
      },
    });
  }

  cancel(): void {
    if (this.mode() === 'edit') {
      void this.router.navigate(['/ns', this.editSlug]);
      return;
    }
    void this.router.navigate(['/']);
  }

  submit(): void {
    this.saveError.set(null);
    const displayName = this.nsDisplayName.trim();
    if (!displayName) {
      this.saveError.set('Display name is required.');
      return;
    }

    if (this.mode() === 'create') {
      const slug = this.nsSlugModel.trim();
      if (!slug) {
        this.saveError.set('Slug is required.');
        return;
      }
      this.saveBusy.set(true);
      this.api
        .createNamespace({
          slug,
          displayName,
          description: this.nsDescription.trim() || null,
          visibility: this.nsVisibility,
        })
        .subscribe({
          next: () => {
            this.saveBusy.set(false);
            void this.router.navigate(['/ns', slug]);
          },
          error: (err) => {
            this.saveBusy.set(false);
            this.saveError.set(registryApiErrorMessage(err));
          },
        });
      return;
    }

    this.saveBusy.set(true);
    this.api
      .updateNamespace(this.editSlug, {
        displayName,
        description: this.nsDescription.trim() || null,
        visibility: this.nsVisibility,
      })
      .subscribe({
        next: () => {
          this.saveBusy.set(false);
          void this.router.navigate(['/ns', this.editSlug]);
        },
        error: (err) => {
          this.saveBusy.set(false);
          this.saveError.set(registryApiErrorMessage(err));
        },
      });
  }
}
