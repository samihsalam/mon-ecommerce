import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

// Placeholder home — the real catalogue page lands in Epic 3.
@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink],
  template: `
    <main class="mx-auto max-w-md px-4 py-16 text-center">
      <h1 class="font-heading text-3xl text-text mb-8">MonEcommerce</h1>
      <a routerLink="/inscription" class="inline-block rounded-button bg-text text-white px-6 py-2 font-semibold">
        Créer un compte
      </a>
    </main>
  `,
})
export class HomeComponent {}
