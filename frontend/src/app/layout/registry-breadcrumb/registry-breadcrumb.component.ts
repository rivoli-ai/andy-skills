import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter, Subscription } from 'rxjs';

export interface RegistryCrumb {
  label: string;
  /** When set, segment is a link (non-leaf). */
  routerLink: string | null;
}

@Component({
  selector: 'app-registry-breadcrumb',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './registry-breadcrumb.component.html',
  styleUrl: './registry-breadcrumb.component.css',
})
export class RegistryBreadcrumbComponent implements OnInit, OnDestroy {
  private readonly router = inject(Router);
  private sub?: Subscription;

  readonly segments = signal<RegistryCrumb[]>([]);

  ngOnInit(): void {
    this.applyUrl(this.router.url);
    this.sub = this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(() => this.applyUrl(this.router.url));
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  private applyUrl(url: string): void {
    const path = url.split('?')[0].split('#')[0];
    const list: RegistryCrumb[] = [{ label: 'registry', routerLink: '/' }];

    if (path === '/' || path === '') {
      list.push({ label: 'namespaces', routerLink: null });
    } else if (path.startsWith('/skills')) {
      if (path === '/skills') {
        list.push({ label: 'skills', routerLink: null });
      } else if (path === '/skills/new') {
        list.push({ label: 'skills', routerLink: '/skills' });
        list.push({ label: 'New skill', routerLink: null });
      } else {
        const rest = path.slice('/skills/'.length);
        const parts = rest.split('/').filter(Boolean).map((p) => decodeURIComponent(p));
        list.push({ label: 'skills', routerLink: '/skills' });
        if (parts.length >= 2) {
          const [nsSeg, skillSeg] = parts;
          list.push({
            label: nsSeg,
            routerLink: `/ns/${encodeURIComponent(nsSeg)}`,
          });
          list.push({ label: skillSeg, routerLink: null });
        } else if (parts.length === 1) {
          list.push({ label: parts[0], routerLink: null });
        }
      }
    } else if (path.startsWith('/settings')) {
      list.push({ label: 'settings', routerLink: null });
    } else if (path.startsWith('/ns/')) {
      const restEncoded = path.slice('/ns/'.length);
      const rest = decodeURIComponent(restEncoded);

      list.push({ label: 'namespaces', routerLink: '/' });

      if (rest === 'new') {
        list.push({ label: 'New namespace', routerLink: null });
      } else {
        const segments = rest.split('/').filter(Boolean);
        const last = segments[segments.length - 1];
        if (segments.length >= 2 && last === 'edit') {
          const slug = segments.slice(0, -1).join('/');
          list.push({
            label: slug || 'namespace',
            routerLink: `/ns/${encodeURIComponent(slug)}`,
          });
          list.push({ label: 'Edit', routerLink: null });
        } else {
          list.push({ label: rest || 'namespace', routerLink: null });
        }
      }
    } else {
      list.push({ label: 'view', routerLink: null });
    }

    this.segments.set(list);
  }
}
