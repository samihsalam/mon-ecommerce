import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../shared/services/api_client.dart';

class ProductSummary {
  const ProductSummary({
    required this.id,
    required this.name,
    required this.priceInCents,
    required this.categoryId,
    required this.categoryName,
    required this.inStock,
  });

  final String id;
  final String name;
  final int priceInCents;
  final String categoryId;
  final String categoryName;
  final bool inStock;

  factory ProductSummary.fromJson(Map<String, dynamic> json) => ProductSummary(
        id: json['id'] as String,
        name: json['name'] as String,
        priceInCents: json['priceInCents'] as int,
        categoryId: json['categoryId'] as String,
        categoryName: json['categoryName'] as String,
        inStock: json['inStock'] as bool,
      );
}

class CategorySummary {
  const CategorySummary({required this.id, required this.name, required this.slug});

  final String id;
  final String name;
  final String slug;

  factory CategorySummary.fromJson(Map<String, dynamic> json) => CategorySummary(
        id: json['id'] as String,
        name: json['name'] as String,
        slug: json['slug'] as String,
      );
}

class SearchSuggestions {
  const SearchSuggestions({this.categories = const [], this.products = const []});

  final List<String> categories;
  final List<String> products;

  factory SearchSuggestions.fromJson(Map<String, dynamic> json) => SearchSuggestions(
        categories: (json['categories'] as List<dynamic>).cast<String>(),
        products: (json['products'] as List<dynamic>).cast<String>(),
      );
}

class CatalogueState {
  const CatalogueState({
    this.results = const [],
    this.totalCount = 0,
    this.isSearching = false,
    this.searchError,
    this.suggestions = const SearchSuggestions(),
    this.isLoadingSuggestions = false,
    this.categories = const [],
  });

  final List<ProductSummary> results;
  final int totalCount;
  final bool isSearching;
  final String? searchError;
  final SearchSuggestions suggestions;
  final bool isLoadingSuggestions;
  final List<CategorySummary> categories;

  // searchError is deliberately NOT sticky (always overwritten, no `?? this.searchError`
  // fallback) — same reasoning as OrdersState.selectedOrder: a second search must never render
  // the FIRST search's stale error for a frame while the new request is in flight.
  CatalogueState copyWith({
    List<ProductSummary>? results,
    int? totalCount,
    bool? isSearching,
    String? searchError,
    SearchSuggestions? suggestions,
    bool? isLoadingSuggestions,
    List<CategorySummary>? categories,
  }) {
    return CatalogueState(
      results: results ?? this.results,
      totalCount: totalCount ?? this.totalCount,
      isSearching: isSearching ?? this.isSearching,
      searchError: searchError,
      suggestions: suggestions ?? this.suggestions,
      isLoadingSuggestions: isLoadingSuggestions ?? this.isLoadingSuggestions,
      categories: categories ?? this.categories,
    );
  }
}

class CatalogueNotifier extends Notifier<CatalogueState> {
  // Monotonic request counters — not request cancellation, just staleness detection. Dio isn't
  // given a CancelToken here, so an earlier search/suggestions call that resolves AFTER a later
  // one (out-of-order network response) would otherwise unconditionally overwrite the state with
  // stale data. Each call captures its own id before awaiting; if the counter moved on by the
  // time the response arrives, the result is discarded.
  int _searchRequestId = 0;
  int _suggestionsRequestId = 0;

  @override
  CatalogueState build() => const CatalogueState();

  Future<void> search(String term) async {
    final requestId = ++_searchRequestId;
    state = state.copyWith(isSearching: true, searchError: null);

    try {
      final dio = ref.read(apiClientProvider);
      final response = await dio.get<Map<String, dynamic>>(
        '/api/v1/products',
        queryParameters: {'search': term, 'pageNumber': 1, 'pageSize': 20},
      );
      if (requestId != _searchRequestId) {
        return;
      }
      final data = response.data!;
      final items =
          (data['items'] as List<dynamic>).map((p) => ProductSummary.fromJson(p as Map<String, dynamic>)).toList();
      state = state.copyWith(isSearching: false, results: items, totalCount: data['totalCount'] as int);
    } catch (e) {
      if (requestId != _searchRequestId) {
        return;
      }
      state = state.copyWith(
        isSearching: false,
        results: [],
        searchError: 'Impossible de charger les résultats. Veuillez réessayer.',
      );
    }
  }

  Future<void> loadSuggestions(String term) async {
    final requestId = ++_suggestionsRequestId;
    state = state.copyWith(isLoadingSuggestions: true);

    try {
      final dio = ref.read(apiClientProvider);
      final response = await dio.get<Map<String, dynamic>>(
        '/api/v1/products/suggestions',
        queryParameters: {'search': term},
      );
      if (requestId != _suggestionsRequestId) {
        return;
      }
      state = state.copyWith(isLoadingSuggestions: false, suggestions: SearchSuggestions.fromJson(response.data!));
    } catch (e) {
      if (requestId != _suggestionsRequestId) {
        return;
      }
      state = state.copyWith(isLoadingSuggestions: false, suggestions: const SearchSuggestions());
    }
  }

  void clearSuggestions() {
    // Bumping the counter also discards any suggestions request already in flight — otherwise it
    // could still resolve after the box was cleared and repopulate the dropdown.
    _suggestionsRequestId++;
    state = state.copyWith(suggestions: const SearchSuggestions(), isLoadingSuggestions: false);
  }

  Future<void> loadCategories() async {
    if (state.categories.isNotEmpty) {
      return;
    }

    try {
      final dio = ref.read(apiClientProvider);
      final response = await dio.get<List<dynamic>>('/api/v1/products/categories');
      final categories =
          response.data!.map((c) => CategorySummary.fromJson(c as Map<String, dynamic>)).toList();
      state = state.copyWith(categories: categories);
    } catch (e) {
      // Empty-state category links are a nice-to-have, not critical — fail silently.
    }
  }
}

final catalogueProvider = NotifierProvider<CatalogueNotifier, CatalogueState>(CatalogueNotifier.new);
