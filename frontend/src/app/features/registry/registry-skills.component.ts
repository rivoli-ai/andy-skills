import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
import {
  NamespaceDto,
  PackageSummaryDto,
  RegistryApiService,
  registryApiErrorMessage,
} from '../../core/services/registry-api.service';

export interface NamespaceSkillsGroup {
  namespace: NamespaceDto;
  packages: PackageSummaryDto[];
}

export interface SkillFlatRow {
  namespace: NamespaceDto;
  pkg: PackageSummaryDto;
}

const PREFS_KEY = 'skills-catalog-preferences';

interface SkillsCatalogPrefs {
  viewMode?: 'grid' | 'list';
  groupByNamespace?: boolean;
  collapsedNsIds?: string[];
  selectedNamespaceSlug?: string;
  visibilityFilter?: string;
  sortBy?: 'title' | 'slug' | 'createdAt' | 'latestVersion';
  sortOrder?: 'asc' | 'desc';
  filterQuery?: string;
}

@Component({
  selector: 'app-registry-skills',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './registry-skills.component.html',
  styleUrl: './registry-skills.component.css',
})
export class RegistrySkillsComponent implements OnInit, OnDestroy {
  private readonly api = inject(RegistryApiService);
  private prefsSaveTimer?: ReturnType<typeof setTimeout>;

  readonly filterQuery = signal('');
  readonly groups = signal<NamespaceSkillsGroup[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly deleteBusyId = signal<string | null>(null);

  readonly viewMode = signal<'grid' | 'list'>('list');
  readonly groupByNamespace = signal(true);
  /** Namespace IDs with collapsed content */
  readonly collapsedNsIds = signal<ReadonlySet<string>>(new Set());
  readonly selectedNamespaceSlug = signal('');
  readonly visibilityFilter = signal<'all' | string>('all');
  readonly sortBy = signal<'title' | 'slug' | 'createdAt' | 'latestVersion'>('title');
  readonly sortOrder = signal<'asc' | 'desc'>('asc');

  readonly totalSkills = computed(() =>
    this.groups().reduce((n, g) => n + g.packages.length, 0),
  );

  readonly namespaceOptions = computed(() =>
    [...this.groups()].sort((a, b) =>
      a.namespace.displayName.localeCompare(b.namespace.displayName, undefined, { sensitivity: 'base' }),
    ),
  );

  readonly visibilityOptions = computed(() => {
    const s = new Set<string>();
    for (const g of this.groups()) {
      if (g.namespace.visibility) {
        s.add(g.namespace.visibility);
      }
    }
    return [...s].sort((a, b) => a.localeCompare(b));
  });

  readonly processedGroups = computed(() => {
    let list = this.groups().map((g) => ({
      namespace: g.namespace,
      packages: [...g.packages],
    }));

    const vf = this.visibilityFilter();
    if (vf !== 'all') {
      list = list.filter((g) => g.namespace.visibility === vf);
    }

    const sns = this.selectedNamespaceSlug().trim();
    if (sns) {
      list = list.filter((g) => g.namespace.slug === sns);
    }

    const q = this.filterQuery().trim().toLowerCase();
    if (q) {
      list = list
        .map((g) => {
          const nsMatch = this.namespaceMatchesQuery(g.namespace, q);
          const pkgs = nsMatch
            ? g.packages
            : g.packages.filter(
                (p) =>
                  p.slug.toLowerCase().includes(q) ||
                  p.title.toLowerCase().includes(q) ||
                  (p.description ?? '').toLowerCase().includes(q),
              );
          return { ...g, packages: pkgs };
        })
        .filter((g) => g.packages.length > 0 || this.namespaceMatchesQuery(g.namespace, q));
    }

    list = list.map((g) => ({
      ...g,
      packages: this.sortPackages(g.packages),
    }));

    list.sort((a, b) =>
      a.namespace.displayName.localeCompare(b.namespace.displayName, undefined, { sensitivity: 'base' }),
    );

    return list;
  });

  readonly flatSkills = computed(() => {
    const rows: SkillFlatRow[] = [];
    for (const g of this.processedGroups()) {
      for (const pkg of g.packages) {
        rows.push({ namespace: g.namespace, pkg });
      }
    }
    rows.sort((a, b) => this.compareSkills(a.pkg, b.pkg));
    return rows;
  });

  readonly filteredSkillCount = computed(() =>
    this.groupByNamespace()
      ? this.processedGroups().reduce((n, g) => n + g.packages.length, 0)
      : this.flatSkills().length,
  );

  ngOnInit(): void {
    this.loadPreferences();
    this.reload();
  }

  ngOnDestroy(): void {
    clearTimeout(this.prefsSaveTimer);
    this.savePreferences();
  }

  private namespaceMatchesQuery(ns: NamespaceDto, q: string): boolean {
    return (
      ns.slug.toLowerCase().includes(q) ||
      ns.displayName.toLowerCase().includes(q) ||
      (ns.description ?? '').toLowerCase().includes(q)
    );
  }

  private sortPackages(pkgs: PackageSummaryDto[]): PackageSummaryDto[] {
    const copy = [...pkgs];
    copy.sort((a, b) => this.compareSkills(a, b));
    return copy;
  }

  private compareSkills(a: PackageSummaryDto, b: PackageSummaryDto): number {
    const mult = this.sortOrder() === 'asc' ? 1 : -1;
    let c = 0;
    switch (this.sortBy()) {
      case 'title':
        c = a.title.localeCompare(b.title, undefined, { sensitivity: 'base' });
        break;
      case 'slug':
        c = a.slug.localeCompare(b.slug, undefined, { sensitivity: 'base' });
        break;
      case 'createdAt':
        c = new Date(a.createdAtUtc).getTime() - new Date(b.createdAtUtc).getTime();
        break;
      case 'latestVersion':
        c = (a.latestVersion ?? '').localeCompare(b.latestVersion ?? '', undefined, {
          sensitivity: 'base',
          numeric: true,
        });
        break;
    }
    return c * mult;
  }

  namespaceHue(namespaceSlug: string): number {
    let h = 0;
    for (let i = 0; i < namespaceSlug.length; i++) {
      h = (h + namespaceSlug.charCodeAt(i) * (i + 1)) % 360;
    }
    return h;
  }

  isCollapsed(nsId: string): boolean {
    return this.collapsedNsIds().has(nsId);
  }

  toggleGroupCollapse(nsId: string): void {
    const next = new Set(this.collapsedNsIds());
    if (next.has(nsId)) {
      next.delete(nsId);
    } else {
      next.add(nsId);
    }
    this.collapsedNsIds.set(next);
    this.scheduleSavePreferences();
  }

  setViewMode(mode: 'grid' | 'list'): void {
    this.viewMode.set(mode);
    this.scheduleSavePreferences();
  }

  toggleGrouping(): void {
    this.groupByNamespace.update((v) => !v);
    this.scheduleSavePreferences();
  }

  toggleSortOrder(): void {
    this.sortOrder.update((o) => (o === 'asc' ? 'desc' : 'asc'));
    this.scheduleSavePreferences();
  }

  onSortFieldChange(value: string): void {
    const field = value as 'title' | 'slug' | 'createdAt' | 'latestVersion';
    if (this.sortBy() === field) {
      this.toggleSortOrder();
    } else {
      this.sortBy.set(field);
      this.sortOrder.set('desc');
    }
    this.scheduleSavePreferences();
  }

  onNamespaceFilterChange(slug: string): void {
    this.selectedNamespaceSlug.set(slug);
    this.scheduleSavePreferences();
  }

  /** Prefill namespace on /skills/new when a single namespace is selected in filters. */
  newSkillQueryParams(): Record<string, string> {
    const ns = this.selectedNamespaceSlug().trim();
    return ns ? { namespace: ns } : {};
  }

  onVisibilityChange(value: string): void {
    this.visibilityFilter.set(value === 'all' ? 'all' : value);
    this.scheduleSavePreferences();
  }

  filterShort(v: string): string {
    if (v === 'OrgVisible') {
      return 'Org';
    }
    return v === 'Private' ? 'Private' : v;
  }

  clearSearch(): void {
    this.filterQuery.set('');
    this.scheduleSavePreferences();
  }

  onSearchInput(value: string): void {
    this.filterQuery.set(value);
    this.scheduleSavePreferences();
  }

  private scheduleSavePreferences(): void {
    clearTimeout(this.prefsSaveTimer);
    this.prefsSaveTimer = setTimeout(() => this.savePreferences(), 500);
  }

  private loadPreferences(): void {
    try {
      const raw = localStorage.getItem(PREFS_KEY);
      if (!raw) {
        return;
      }
      const p = JSON.parse(raw) as SkillsCatalogPrefs;
      if (p.viewMode === 'grid' || p.viewMode === 'list') {
        this.viewMode.set(p.viewMode);
      }
      if (typeof p.groupByNamespace === 'boolean') {
        this.groupByNamespace.set(p.groupByNamespace);
      }
      if (Array.isArray(p.collapsedNsIds)) {
        this.collapsedNsIds.set(new Set(p.collapsedNsIds));
      }
      if (typeof p.selectedNamespaceSlug === 'string') {
        this.selectedNamespaceSlug.set(p.selectedNamespaceSlug);
      }
      if (typeof p.visibilityFilter === 'string') {
        this.visibilityFilter.set(p.visibilityFilter);
      }
      if (
        p.sortBy === 'title' ||
        p.sortBy === 'slug' ||
        p.sortBy === 'createdAt' ||
        p.sortBy === 'latestVersion'
      ) {
        this.sortBy.set(p.sortBy);
      }
      if (p.sortOrder === 'asc' || p.sortOrder === 'desc') {
        this.sortOrder.set(p.sortOrder);
      }
      if (typeof p.filterQuery === 'string') {
        this.filterQuery.set(p.filterQuery);
      }
    } catch {
      /* ignore */
    }
  }

  private savePreferences(): void {
    try {
      localStorage.setItem(
        PREFS_KEY,
        JSON.stringify({
          viewMode: this.viewMode(),
          groupByNamespace: this.groupByNamespace(),
          collapsedNsIds: Array.from(this.collapsedNsIds()),
          selectedNamespaceSlug: this.selectedNamespaceSlug(),
          visibilityFilter: this.visibilityFilter(),
          sortBy: this.sortBy(),
          sortOrder: this.sortOrder(),
          filterQuery: this.filterQuery(),
        }),
      );
    } catch {
      /* ignore */
    }
  }

  confirmDeleteSkill(namespaceSlug: string, pkg: PackageSummaryDto): void {
    if (!namespaceSlug || this.deleteBusyId()) {
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
    this.api.deletePackage(namespaceSlug, pkg.slug).subscribe({
      next: () => {
        this.deleteBusyId.set(null);
        this.reload();
      },
      error: (err) => {
        this.deleteBusyId.set(null);
        window.alert(registryApiErrorMessage(err));
      },
    });
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api
      .listNamespaces()
      .pipe(
        switchMap((namespaces) => {
          if (namespaces.length === 0) {
            return of([] as NamespaceSkillsGroup[]);
          }
          const sorted = [...namespaces].sort((a, b) =>
            a.displayName.localeCompare(b.displayName, undefined, { sensitivity: 'base' }),
          );
          return forkJoin(
            sorted.map((ns) =>
              this.api.listPackages(ns.slug).pipe(
                catchError(() => of([] as PackageSummaryDto[])),
                map((packages) => ({ namespace: ns, packages })),
              ),
            ),
          );
        }),
      )
      .subscribe({
        next: (loaded) => {
          this.groups.set(loaded);
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(registryApiErrorMessage(err));
          this.loading.set(false);
        },
      });
  }
}
