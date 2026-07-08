import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { adminGuard } from './core/admin.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/login/login').then((m) => m.Login),
  },
  {
    path: 'board',
    loadComponent: () => import('./features/board/board').then((m) => m.BoardView),
    canActivate: [authGuard],
  },
  {
    path: 'timeline',
    loadComponent: () => import('./features/timeline/timeline').then((m) => m.TimelineView),
    canActivate: [authGuard],
  },
  {
    path: 'team',
    loadComponent: () => import('./features/team/team').then((m) => m.TeamView),
    canActivate: [adminGuard],
  },
  {
    path: 'capacity',
    loadComponent: () => import('./features/capacity/capacity').then((m) => m.CapacityView),
    canActivate: [adminGuard],
  },
  { path: '', pathMatch: 'full', redirectTo: 'board' },
  { path: '**', redirectTo: 'board' },
];
