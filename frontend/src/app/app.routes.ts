import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { MainLayoutComponent } from './layout/main-layout.component';
import { NamespaceDetailComponent } from './features/registry/namespace-detail.component';
import { NamespaceFormComponent } from './features/registry/namespace-form.component';
import { RegistryHomeComponent } from './features/registry/registry-home.component';
import { RegistrySkillsComponent } from './features/registry/registry-skills.component';
import { RegistrySettingsComponent } from './features/registry/registry-settings.component';
import { SkillCreateComponent } from './features/registry/skill-create.component';
import { SkillDetailComponent } from './features/registry/skill-detail.component';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./core/auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'auth/callback/:provider',
    loadComponent: () => import('./core/auth/callback/callback.component').then((m) => m.CallbackComponent),
  },
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: '', component: RegistryHomeComponent },
      { path: 'skills/new', component: SkillCreateComponent },
      {
        path: 'skills/:namespaceSlug/:skillSlug/edit',
        component: SkillCreateComponent,
        data: { skillFormMode: 'edit' as const },
      },
      {
        path: 'skills/:namespaceSlug/:skillSlug',
        component: SkillDetailComponent,
      },
      { path: 'skills', component: RegistrySkillsComponent },
      { path: 'settings', component: RegistrySettingsComponent },
      {
        path: 'ns/new',
        component: NamespaceFormComponent,
        data: { namespaceFormMode: 'create' as const },
      },
      {
        path: 'ns/:slug/edit',
        component: NamespaceFormComponent,
        data: { namespaceFormMode: 'edit' as const },
      },
      { path: 'ns/:slug', component: NamespaceDetailComponent },
    ],
  },
];
