import 'package:flutter/material.dart';

import '../../../app/theme/design_tokens.dart';
import '../providers/catalogue_provider.dart';

// Distinguishes "user chose a category (or explicitly cleared it)" from "sheet dismissed without
// any choice" (scrim tap / swipe down, which Navigator.pop() reports as a bare `null` result) —
// a bare `String?` return from showModalBottomSheet can't tell those two cases apart, since
// `categoryId: null` is itself a valid, meaningful choice ("no category filter").
class CatalogueFilterResult {
  const CatalogueFilterResult(this.categoryId);

  final String? categoryId;
}

// Category-only, mirroring Angular's FilterChipBarComponent (Story 3.3) — material/color have no
// backend endpoint enumerating their distinct in-use values, and price range has no predefined
// bucket concept anywhere in this codebase. See Story 3.4's Dev Notes for the full reasoning.
class CatalogueFilterSheet extends StatelessWidget {
  const CatalogueFilterSheet({super.key, required this.categories, required this.activeCategoryId});

  final List<CategorySummary> categories;
  final String? activeCategoryId;

  static Future<CatalogueFilterResult?> show(
    BuildContext context, {
    required List<CategorySummary> categories,
    required String? activeCategoryId,
  }) {
    return showModalBottomSheet<CatalogueFilterResult>(
      context: context,
      builder: (context) => CatalogueFilterSheet(categories: categories, activeCategoryId: activeCategoryId),
    );
  }

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.all(AppTokens.space16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text('Filtrer par catégorie', style: TextStyle(fontWeight: FontWeight.w600, fontSize: 16)),
                if (activeCategoryId != null)
                  TextButton(
                    onPressed: () => Navigator.of(context).pop(const CatalogueFilterResult(null)),
                    child: const Text('Tout effacer'),
                  ),
              ],
            ),
            const SizedBox(height: AppTokens.space16),
            Wrap(
              spacing: AppTokens.space8,
              runSpacing: AppTokens.space8,
              children: [
                // FilterChip already provides well-formed built-in semantics (its own label from
                // the Text child, plus selected state) — an extra wrapping Semantics(label:,
                // selected:) here duplicated that into a second, redundant announcement rather
                // than adding anything.
                for (final category in categories)
                  FilterChip(
                    label: Text(category.name),
                    selected: activeCategoryId == category.id,
                    // Tapping a chip immediately applies the filter and closes the sheet — AC's
                    // "selection is confirmed" is read as "chip tapped": a single-select
                    // category chip needs no separate confirm step. Re-tapping the already-
                    // active chip toggles it off (same UX as Angular's FilterChipBar).
                    onSelected: (_) => Navigator.of(context).pop(
                      CatalogueFilterResult(activeCategoryId == category.id ? null : category.id),
                    ),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
