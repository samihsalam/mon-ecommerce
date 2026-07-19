import { Component, inject, signal, afterNextRender } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthStore } from '../auth/auth.store';

// Placeholder home — the real catalogue page lands in Epic 3.
@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink],
  template: `
    <main class="mx-auto max-w-md px-4 py-16 text-center">
      <h1 class="font-heading text-3xl text-text mb-8">MonEcommerce</h1>
      @if (hydrated() && authStore.isAuthenticated()) {
        <button
          type="button"
          (click)="onLogout()"
          class="inline-block rounded-button bg-text text-white px-6 py-2 font-semibold"
        >
          Se déconnecter
        </button>
      } @else {
        <div class="flex flex-col gap-4 items-center">
          <a routerLink="/inscription" class="inline-block rounded-button bg-text text-white px-6 py-2 font-semibold">
            Créer un compte
          </a>
          <a routerLink="/connexion" class="text-sm text-text underline"> Se connecter </a>
        </div>
      }
    </main>
  `,
})
export class HomeComponent {
  protected readonly authStore = inject(AuthStore);

  // The server never knows isAuthenticated (no token on the server), so it always renders
  // the logged-out branch. If the client's first render used the store's real value
  // immediately, an already-logged-in user reloading the page would hydrate onto
  // structurally different markup than the server sent — a hydration mismatch. Gating on
  // `hydrated` (flipped one tick after the client's first render, via afterNextRender,
  // which never runs during SSR) keeps the client's initial render identical to the
  // server's, then swaps in the real state right after.
  protected readonly hydrated = signal(false);

  constructor() {
    afterNextRender(() => this.hydrated.set(true));
  }

  protected async onLogout(): Promise<void> {
    await this.authStore.logout();
  }
}
