import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

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
  { path: '', pathMatch: 'full', redirectTo: 'board' },
  { path: '**', redirectTo: 'board' },
];
