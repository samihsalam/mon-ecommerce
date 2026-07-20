import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/theme/design_tokens.dart';
import '../providers/catalogue_provider.dart';
import '../widgets/catalogue_filter_sheet.dart';
import '../widgets/product_card.dart';
import '../widgets/product_card_skeleton.dart';

const _skeletonCount = 8;

// childAspectRatio (width/height) budgets each cell's height for: a 3:4 image (≈1.33×width) plus
// a text block below it (spacing + up to 2 name lines + spacing + price + an occasional "Rupture
// de stock" line). 0.62 (≈1.61×width total) left only ≈0.28×width for that whole text block —
// not enough once a product name actually wraps to 2 lines, causing a RenderFlex overflow. 0.52
// (≈1.92×width total, ≈0.59×width for text) leaves comfortable headroom for the worst case.
// Shared by both the skeleton and real grids so they always stay pixel-identical (AC's own "same
// 3:4 ratio" requirement for the skeleton).
const _gridDelegate = SliverGridDelegateWithFixedCrossAxisCount(
  crossAxisCount: 2,
  mainAxisSpacing: AppTokens.space16,
  crossAxisSpacing: AppTokens.space16,
  childAspectRatio: 0.52,
);

class CatalogueScreen extends ConsumerStatefulWidget {
  const CatalogueScreen({super.key, this.categoryId});

  final String? categoryId;

  @override
  ConsumerState<CatalogueScreen> createState() => _CatalogueScreenState();
}

class _CatalogueScreenState extends ConsumerState<CatalogueScreen> {
  @override
  void initState() {
    super.initState();
    // The category filter is carried in the route's `categoryId` query param (not just provider
    // state) so it survives navigating to a product detail and back (browser/OS-level back
    // restores the URL) — same mechanism as Angular's CatalogueComponent (Story 3.3).
    //
    // Called directly, NOT wrapped in Future.microtask: catalogueProvider is shared with
    // SearchScreen (Story 3.2), so this screen's first frame(s) could otherwise render whatever
    // that OTHER screen last left in the shared state (a stale category label, a stale filter
    // badge, even a flash of the previous screen's product cards) — a microtask only runs after
    // the whole first frame (build/layout/paint) completes. browse() sets isSearching: true and
    // clears/sets activeCategoryId synchronously, before its own first `await` — calling it
    // directly here lets that synchronous prefix run during initState, before the first build(),
    // so the very first frame already shows the skeleton loader instead of stale content.
    ref.read(catalogueProvider.notifier).loadCategories();
    ref.read(catalogueProvider.notifier).browse(categoryId: widget.categoryId);
  }

  @override
  void didUpdateWidget(CatalogueScreen oldWidget) {
    super.didUpdateWidget(oldWidget);
    // go_router doesn't guarantee a fresh State when navigating to the same route pattern with
    // only the query param changed (e.g. context.go('/catalogue?categoryId=B') while already on
    // '/catalogue?categoryId=A')) — if the State IS reused, initState() alone would never re-fetch
    // for the new category. didUpdateWidget covers that case; it's a no-op if the framework
    // instead creates a fresh widget/State (initState already handled that).
    if (widget.categoryId != oldWidget.categoryId) {
      ref.read(catalogueProvider.notifier).browse(categoryId: widget.categoryId);
    }
  }

  Future<void> _openFilterSheet() async {
    final catalogueState = ref.read(catalogueProvider);
    final result = await CatalogueFilterSheet.show(
      context,
      categories: catalogueState.categories,
      activeCategoryId: catalogueState.activeCategoryId,
    );
    if (result == null || !mounted) {
      return;
    }
    context.go(result.categoryId == null ? '/catalogue' : '/catalogue?categoryId=${result.categoryId}');
  }

  void _loadMore() {
    final catalogueState = ref.read(catalogueProvider);
    // Guards against a double-tap firing two browse() calls before the first state update swaps
    // the button for a CircularProgressIndicator — otherwise benign (the shared staleness guard
    // in browse() already discards whichever response loses the race), but a wasted duplicate
    // network call is cheap to avoid outright.
    if (catalogueState.isLoadingMore) {
      return;
    }
    ref
        .read(catalogueProvider.notifier)
        .browse(categoryId: catalogueState.activeCategoryId, pageNumber: catalogueState.pageNumber + 1);
  }

  String _resultsLabel(CatalogueState state) {
    // Iterable.firstOrNull isn't available without importing package:collection, which isn't a
    // dependency here — a manual where()+isEmpty check avoids adding one for a single call site.
    final matches = state.categories.where((c) => c.id == state.activeCategoryId);
    final noun = matches.isEmpty ? 'produits' : matches.first.name.toLowerCase();
    return '${state.totalCount} $noun trouvés';
  }

  @override
  Widget build(BuildContext context) {
    final catalogueState = ref.watch(catalogueProvider);
    final activeFilterCount = catalogueState.activeCategoryId != null ? 1 : 0;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Catalogue'),
        actions: [
          // IconButton already derives its semantic label from `tooltip` — a second, explicit
          // Semantics(label:, button:) wrapper here duplicated that into a competing label source
          // rather than adding anything, the same redundancy pattern fixed on the filter chips.
          IconButton(
            icon: activeFilterCount > 0
                ? Badge(label: Text('$activeFilterCount'), child: const Icon(Icons.filter_list))
                : const Icon(Icons.filter_list),
            tooltip: activeFilterCount > 0 ? 'Filtres ($activeFilterCount)' : 'Filtrer',
            onPressed: _openFilterSheet,
          ),
        ],
      ),
      body: Padding(
        padding: const EdgeInsets.all(AppTokens.space16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (!catalogueState.isSearching && catalogueState.searchError == null)
              Semantics(
                liveRegion: true,
                child: Text(_resultsLabel(catalogueState), style: const TextStyle(color: AppTokens.textSecondaryColor)),
              ),
            const SizedBox(height: AppTokens.space16),
            Expanded(child: _buildBody(catalogueState)),
          ],
        ),
      ),
    );
  }

  Widget _buildBody(CatalogueState state) {
    if (state.searchError != null) {
      return Center(child: Text(state.searchError!, style: const TextStyle(color: AppTokens.errorColor)));
    }

    if (state.isSearching) {
      return GridView.builder(
        gridDelegate: _gridDelegate,
        itemCount: _skeletonCount,
        itemBuilder: (context, index) => const ProductCardSkeleton(),
      );
    }

    if (state.results.isEmpty) {
      return const Center(child: Text('Aucun produit dans cette catégorie.'));
    }

    return GridView.builder(
      gridDelegate: _gridDelegate,
      itemCount: state.results.length + (state.pageNumber < state.totalPages ? 1 : 0),
      itemBuilder: (context, index) {
        if (index >= state.results.length) {
          return Center(
            child: state.isLoadingMore
                ? const CircularProgressIndicator()
                : TextButton(onPressed: _loadMore, child: const Text('Charger plus')),
          );
        }
        return ProductCard(product: state.results[index]);
      },
    );
  }
}
