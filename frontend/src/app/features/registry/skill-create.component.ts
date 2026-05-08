import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  NamespaceDto,
  RegistryApiService,
  registryApiErrorMessage,
} from '../../core/services/registry-api.service';

@Component({
  selector: 'app-skill-create',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './skill-create.component.html',
  styleUrl: './skill-create.component.css',
})
export class SkillCreateComponent implements OnInit {
  private readonly api = inject(RegistryApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly mode = signal<'create' | 'edit'>('create');
  readonly loadingNs = signal(true);
  readonly loadingSkill = signal(false);
  readonly pageError = signal<string | null>(null);
  readonly saveError = signal<string | null>(null);
  readonly busy = signal(false);

  readonly namespaces = signal<NamespaceDto[]>([]);

  /** Route params for edit mode (Cancel / Back links). */
  readonly editNsSlug = signal('');
  readonly editSkillSlug = signal('');

  /** When skill/edit opened from `/ns/:slug` (?from=ns), preserve for back navigation. */
  readonly enteredFromNamespaceDetail = signal(false);

  readonly skillDetailBackQueryParams = computed(() =>
    this.enteredFromNamespaceDetail() ? { from: 'ns' } : {},
  );

  nsSlugModel = '';
  skillSlug = '';
  skillTitle = '';
  skillDescription = '';

  ngOnInit(): void {
    this.enteredFromNamespaceDetail.set(this.route.snapshot.queryParamMap.get('from') === 'ns');

    const modeFromRoute = this.route.snapshot.data['skillFormMode'] as 'edit' | undefined;
    this.mode.set(modeFromRoute === 'edit' ? 'edit' : 'create');
    const nsParam = this.route.snapshot.paramMap.get('namespaceSlug')?.trim() ?? '';
    const skillParam = this.route.snapshot.paramMap.get('skillSlug')?.trim() ?? '';

    if (this.mode() === 'edit') {
      this.editNsSlug.set(nsParam);
      this.editSkillSlug.set(skillParam);
    }

    this.api.listNamespaces().subscribe({
      next: (rows) => {
        const sorted = [...rows].sort((a, b) =>
          a.displayName.localeCompare(b.displayName, undefined, { sensitivity: 'base' }),
        );
        this.namespaces.set(sorted);
        this.loadingNs.set(false);

        if (sorted.length === 0) {
          return;
        }

        if (this.mode() === 'edit') {
          if (!nsParam || !skillParam) {
            this.pageError.set('Invalid skill edit URL.');
            return;
          }
          if (!sorted.some((n) => n.slug === nsParam)) {
            this.pageError.set(
              `You don't have permission to manage skills in namespace "${nsParam}", or it isn't visible to your account. Ask a namespace owner or admin for Admin access.`,
            );
            return;
          }
          this.nsSlugModel = nsParam;
          this.skillSlug = skillParam;
          this.loadingSkill.set(true);
          this.api.listPackages(nsParam).subscribe({
            next: (pkgs) => {
              const pkg = pkgs.find((p) => p.slug === skillParam);
              if (!pkg) {
                this.pageError.set('Skill not found in this namespace.');
                this.loadingSkill.set(false);
                return;
              }
              this.skillTitle = pkg.title;
              this.skillDescription = pkg.description?.trim() ? pkg.description : '';
              this.loadingSkill.set(false);
            },
            error: (err) => {
              this.pageError.set(registryApiErrorMessage(err));
              this.loadingSkill.set(false);
            },
          });
          return;
        }

        const preset = this.route.snapshot.queryParamMap.get('namespace')?.trim();
        if (preset && sorted.some((n) => n.slug === preset)) {
          this.nsSlugModel = preset;
        } else if (sorted.length > 0) {
          this.nsSlugModel = sorted[0].slug;
        }
      },
      error: (err) => {
        this.pageError.set(registryApiErrorMessage(err));
        this.loadingNs.set(false);
      },
    });
  }

  namespaceDisplayLabel(): string {
    const n = this.namespaces().find((x) => x.slug === this.nsSlugModel);
    return n ? `${n.displayName} (${n.slug})` : this.nsSlugModel;
  }

  cancel(): void {
    if (this.mode() === 'edit') {
      void this.router.navigate(['/skills', this.editNsSlug(), this.editSkillSlug()], {
        queryParams: this.skillDetailBackQueryParams(),
      });
    } else {
      void this.router.navigate(['/skills']);
    }
  }

  submit(): void {
    const ns = this.nsSlugModel.trim();
    const slug = this.skillSlug.trim();
    const title = this.skillTitle.trim();
    this.saveError.set(null);
    if (!ns) {
      this.saveError.set('Choose a namespace.');
      return;
    }
    if (!slug || !title) {
      this.saveError.set('Skill slug and title are required.');
      return;
    }
    this.busy.set(true);
    if (this.mode() === 'edit') {
      this.api
        .updatePackage(ns, slug, {
          title,
          description: this.skillDescription.trim() || null,
        })
        .subscribe({
          next: () => {
            this.busy.set(false);
            void this.router.navigate(['/skills', ns, slug], {
              queryParams: this.skillDetailBackQueryParams(),
            });
          },
          error: (err) => {
            this.busy.set(false);
            this.saveError.set(registryApiErrorMessage(err));
          },
        });
      return;
    }

    this.api
      .createPackage(ns, {
        slug,
        title,
        description: this.skillDescription.trim() || null,
      })
      .subscribe({
        next: () => {
          this.busy.set(false);
          void this.router.navigate(['/skills', ns, slug], {
            queryParams: this.skillDetailBackQueryParams(),
          });
        },
        error: (err) => {
          this.busy.set(false);
          this.saveError.set(registryApiErrorMessage(err));
        },
      });
  }
}
