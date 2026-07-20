import { Component, ElementRef, HostListener, inject, OnDestroy, signal } from '@angular/core';
import { Router } from '@angular/router';

import { CatalogueStore } from '../../catalogue.store';

const SUGGESTION_DEBOUNCE_MS = 300;
const MIN_TERM_LENGTH = 2;

@Component({
  selector: 'app-search-bar',
  standalone: true,
  templateUrl: './search-bar.component.html',
  styleUrl: './search-bar.component.scss',
})
export class SearchBarComponent implements OnDestroy {
  private readonly router = inject(Router);
  private readonly elementRef = inject(ElementRef<HTMLElement>);
  protected readonly catalogueStore = inject(CatalogueStore);

  protected readonly term = signal('');
  protected readonly isOpen = signal(false);

  private debounceHandle: ReturnType<typeof setTimeout> | undefined;

  ngOnDestroy(): void {
    // Without this, typing then navigating away within the 300ms debounce window still fires the
    // pending setTimeout after teardown — an unnecessary HTTP call via the root-scoped store, and
    // it keeps this component instance reachable via the timer's closure until it fires.
    clearTimeout(this.debounceHandle);
  }

  protected onInput(value: string): void {
    this.term.set(value);
    clearTimeout(this.debounceHandle);

    const trimmed = value.trim();
    if (trimmed.length < MIN_TERM_LENGTH) {
      this.catalogueStore.clearSuggestions();
      this.isOpen.set(false);
      return;
    }

    this.debounceHandle = setTimeout(() => {
      void this.catalogueStore.loadSuggestions(trimmed);
      this.isOpen.set(true);
    }, SUGGESTION_DEBOUNCE_MS);
  }

  protected onEnter(): void {
    this.submit(this.term());
  }

  protected onEscape(): void {
    this.isOpen.set(false);
  }

  protected selectSuggestion(suggestion: string): void {
    this.submit(suggestion);
  }

  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    if (!this.elementRef.nativeElement.contains(event.target as Node)) {
      this.isOpen.set(false);
    }
  }

  private submit(term: string): void {
    const trimmed = term.trim();
    if (trimmed.length < MIN_TERM_LENGTH) {
      return;
    }

    this.isOpen.set(false);
    void this.router.navigate(['/recherche'], { queryParams: { q: trimmed } });
  }
}
