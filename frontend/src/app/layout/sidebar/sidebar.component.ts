import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { AuthService } from '../../core/services/auth.service';

/**
 * Sidebar navigation — DevPilot-aligned layout; Skill Registry labels.
 */
@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.css',
})
export class SidebarComponent {
  /** Narrow icon rail when true */
  isCollapsed = input(false);
  /** Mobile drawer open state */
  mobileOpen = input(false);

  collapseToggled = output<void>();
  /** Close the mobile drawer after choosing a destination */
  mobileDrawerClose = output<void>();

  constructor(
    public authService: AuthService,
    private readonly router: Router,
    private readonly oidcSecurityService: OidcSecurityService,
  ) {}

  onToggleCollapse(): void {
    this.collapseToggled.emit();
  }

  onMobileNavLinkClick(): void {
    if (this.mobileOpen()) {
      this.mobileDrawerClose.emit();
    }
  }

  logout(): void {
    this.oidcSecurityService.logoffLocalMultiple();
    this.authService.logout();
    void this.router.navigate(['/login']);
  }

  namespacesNavActive(): boolean {
    const path = this.router.url.split('?')[0].split('#')[0];
    return path === '/' || path === '' || path.startsWith('/ns');
  }

  skillsNavActive(): boolean {
    const path = this.router.url.split('?')[0].split('#')[0];
    return path === '/skills' || path.startsWith('/skills/');
  }
}
