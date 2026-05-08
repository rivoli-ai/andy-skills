import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import {
  NamespaceDto,
  PackageSummaryDto,
  RegistryApiService,
  registryApiErrorMessage,
} from '../../core/services/registry-api.service';

@Component({
  selector: 'app-namespace-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './namespace-detail.component.html',
  styleUrl: './namespace-detail.component.css',
})
export class NamespaceDetailComponent implements OnInit, OnDestroy {
  private readonly api = inject(RegistryApiService);
  private readonly route = inject(ActivatedRoute);
  private sub?: Subscription;

  readonly nsSlug = signal('');
  readonly loading = signal(true);
  readonly pageError = signal<string | null>(null);
  readonly packages = signal<PackageSummaryDto[]>([]);
  readonly namespaceMeta = signal<NamespaceDto | null>(null);
  readonly deleteBusyId = signal<string | null>(null);

  /** Primary heading + optional slug subtitle when display name differs from slug. */
  readonly namespaceHeading = computed(() => {
    const slug = this.nsSlug();
    const dn = this.namespaceMeta()?.displayName?.trim();
    if (dn && dn !== slug) {
      return { title: dn, slugLine: slug };
    }
    return { title: slug, slugLine: null as string | null };
  });

  ngOnInit(): void {
    this.sub = this.route.paramMap.subscribe((pm) => {
      const slug = pm.get('slug');
      if (!slug) {
        return;
      }
      this.nsSlug.set(slug);
      this.reloadPackages();
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  reloadPackages(): void {
    const slug = this.nsSlug();
    if (!slug) {
      return;
    }
    this.loading.set(true);
    this.pageError.set(null);
    this.namespaceMeta.set(null);
    forkJoin({
      namespaces: this.api.listNamespaces().pipe(catchError(() => of([] as NamespaceDto[]))),
      packages: this.api.listPackages(slug),
    }).subscribe({
      next: ({ namespaces, packages }) => {
        const meta = namespaces.find((n) => n.slug === slug) ?? null;
        this.namespaceMeta.set(meta);
        this.packages.set(packages);
        this.loading.set(false);
      },
      error: (err) => {
        this.pageError.set(registryApiErrorMessage(err));
        this.loading.set(false);
      },
    });
  }

  confirmDeleteSkill(pkg: PackageSummaryDto): void {
    const ns = this.nsSlug();
    if (!ns || this.deleteBusyId()) {
      return;
    }
    const label = pkg.title.trim() || pkg.slug;
    const ok = window.confirm(
      `Delete skill “${label}” (${pkg.slug}) and all of its versions? This cannot be undone.`,
    );
    if (!ok) {
      return;
    }
    this.deleteBusyId.set(pkg.id);
    this.api.deletePackage(ns, pkg.slug).subscribe({
      next: () => {
        this.deleteBusyId.set(null);
        this.reloadPackages();
      },
      error: (err) => {
        this.deleteBusyId.set(null);
        window.alert(registryApiErrorMessage(err));
      },
    });
  }
}
