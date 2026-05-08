import { Routes } from '@angular/router';
import { MainLayoutComponent } from './layout/main-layout.component';
import { NamespaceDetailComponent } from './features/registry/namespace-detail.component';
import { RegistryHomeComponent } from './features/registry/registry-home.component';

export const routes: Routes = [
  {
    path: '',
    component: MainLayoutComponent,
    children: [
      { path: '', component: RegistryHomeComponent },
      { path: 'ns/:slug', component: NamespaceDetailComponent },
    ],
  },
];
