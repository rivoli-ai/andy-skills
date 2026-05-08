import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeService } from '../core/services/theme.service';
import { RegistryBreadcrumbComponent } from './registry-breadcrumb/registry-breadcrumb.component';
import { SidebarComponent } from './sidebar/sidebar.component';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent, RegistryBreadcrumbComponent],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.css',
})
export class MainLayoutComponent implements OnInit {
  private static readonly SIDEBAR_COLLAPSED_KEY = 'skill-registry-sidebar-collapsed';

  readonly theme = inject(ThemeService);

  readonly sidebarCollapsed = signal(false);
  readonly sidebarMobileOpen = signal(false);

  ngOnInit(): void {
    this.sidebarCollapsed.set(localStorage.getItem(MainLayoutComponent.SIDEBAR_COLLAPSED_KEY) === '1');
  }

  toggleSidebarCollapsed(): void {
    const next = !this.sidebarCollapsed();
    this.sidebarCollapsed.set(next);
    localStorage.setItem(MainLayoutComponent.SIDEBAR_COLLAPSED_KEY, next ? '1' : '0');
  }

  toggleMobileSidebar(): void {
    this.sidebarMobileOpen.update((open) => !open);
  }

  closeMobileSidebar(): void {
    this.sidebarMobileOpen.set(false);
  }

  toggleTheme(): void {
    this.theme.toggle();
  }
}
