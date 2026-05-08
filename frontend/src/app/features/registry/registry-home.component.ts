import { CommonModule } from '@angular/common';
import { Component, HostListener, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NamespaceDto, RegistryApiService, registryApiErrorMessage } from '../../core/services/registry-api.service';

@Component({
  selector: 'app-registry-home',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './registry-home.component.html',
  styleUrl: './registry-home.component.css',
})
export class RegistryHomeComponent implements OnInit, OnDestroy {
  private readonly api = inject(RegistryApiService);

  /** Toolbar search */
  readonly filterQuery = signal('');
  readonly namespaces = signal<NamespaceDto[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly filteredNamespaces = computed(() => {
    const q = this.filterQuery().trim().toLowerCase();
    const rows = this.namespaces();
    if (!q) {
      return rows;
    }
    return rows.filter(
      (n) =>
        n.slug.toLowerCase().includes(q) ||
        n.displayName.toLowerCase().includes(q) ||
        (n.description ?? '').toLowerCase().includes(q),
    );
  });

  readonly flash = signal<{ kind: 'ok' | 'err'; text: string } | null>(null);
  private flashTimer?: ReturnType<typeof setTimeout>;

  readonly deleteTarget = signal<NamespaceDto | null>(null);
  readonly deleteBusy = signal(false);

  ngOnInit(): void {
    this.reloadNamespaces();
  }

  ngOnDestroy(): void {
    clearTimeout(this.flashTimer);
  }

  private showFlash(kind: 'ok' | 'err', text: string): void {
    this.flash.set({ kind, text });
    clearTimeout(this.flashTimer);
    this.flashTimer = setTimeout(() => this.flash.set(null), 4200);
  }

  clearSearch(): void {
    this.filterQuery.set('');
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
          'Cannot reach the API. Run Postgres + the Skill Registry API (port 5289) and retry.',
        );
        this.loading.set(false);
      },
    });
  }

  visibilityShort(v: string): string {
    if (v === 'OrgVisible') {
      return 'Org';
    }
    return v === 'Private' ? 'Private' : v;
  }

  openDeleteConfirm(ns: NamespaceDto): void {
    this.deleteTarget.set(ns);
  }

  closeDeleteConfirm(): void {
    this.deleteTarget.set(null);
    this.deleteBusy.set(false);
  }

  confirmDelete(): void {
    const ns = this.deleteTarget();
    if (!ns) {
      return;
    }
    this.deleteBusy.set(true);
    this.api.deleteNamespace(ns.slug).subscribe({
      next: () => {
        this.deleteBusy.set(false);
        this.closeDeleteConfirm();
        this.showFlash('ok', `Namespace “${ns.slug}” deleted.`);
        this.reloadNamespaces();
      },
      error: (err) => {
        this.deleteBusy.set(false);
        this.showFlash('err', registryApiErrorMessage(err));
      },
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.deleteTarget()) {
      this.closeDeleteConfirm();
    }
  }
}
