import { Component, EventEmitter, Input, Output } from '@angular/core';

import type { CategorySummary } from '../../catalogue.store';

@Component({
  selector: 'app-filter-chip-bar',
  standalone: true,
  templateUrl: './filter-chip-bar.component.html',
  styleUrl: './filter-chip-bar.component.scss',
})
export class FilterChipBarComponent {
  @Input({ required: true }) categories!: CategorySummary[];
  @Input() activeCategoryId: string | null = null;

  @Output() categorySelected = new EventEmitter<string | null>();
  @Output() clearAll = new EventEmitter<void>();

  protected onChipClick(categoryId: string): void {
    // Clicking the already-active chip toggles it off (back to the unfiltered catalogue) —
    // otherwise there'd be no way to deselect a category without hitting "Tout effacer".
    this.categorySelected.emit(this.activeCategoryId === categoryId ? null : categoryId);
  }

  protected onClearAll(): void {
    this.clearAll.emit();
  }
}
