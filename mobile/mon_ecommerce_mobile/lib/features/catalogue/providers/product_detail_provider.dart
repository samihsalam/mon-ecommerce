import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../shared/services/api_client.dart';

class ProductDetail {
  const ProductDetail({
    required this.id,
    required this.name,
    required this.description,
    required this.priceInCents,
    required this.material,
    required this.color,
    required this.dimensions,
    required this.stockQuantity,
    required this.inStock,
    required this.categoryName,
    required this.imageUrls,
  });

  final String id;
  final String name;
  final String description;
  final int priceInCents;
  final String? material;
  final String? color;
  final String? dimensions;
  final int stockQuantity;
  final bool inStock;
  final String categoryName;
  final List<String> imageUrls;

  factory ProductDetail.fromJson(Map<String, dynamic> json) => ProductDetail(
        id: json['id'] as String,
        name: json['name'] as String,
        description: json['description'] as String,
        priceInCents: json['priceInCents'] as int,
        material: json['material'] as String?,
        color: json['color'] as String?,
        dimensions: json['dimensions'] as String?,
        stockQuantity: json['stockQuantity'] as int,
        inStock: json['inStock'] as bool,
        categoryName: json['categoryName'] as String,
        imageUrls: (json['imageUrls'] as List<dynamic>).cast<String>(),
      );
}

class ProductDetailState {
  const ProductDetailState({this.product, this.isLoading = false, this.error});

  final ProductDetail? product;
  final bool isLoading;
  final String? error;

  // error is deliberately NOT sticky (always overwritten) — same reasoning as every other
  // Notifier in this codebase: a second load must never render the FIRST load's stale error.
  // product uses an explicit clearProduct flag (a plain nullable param can't distinguish "leave
  // unchanged" from "set to null") — needed so a fresh loadProduct() call can clear the PREVIOUS
  // product before its own response arrives, the same OrdersState.selectedOrder-flash bug class
  // this codebase already fixed once (see catalogue_provider.dart's clearActiveCategoryId).
  ProductDetailState copyWith({
    ProductDetail? product,
    bool clearProduct = false,
    bool? isLoading,
    String? error,
  }) {
    return ProductDetailState(
      product: clearProduct ? null : (product ?? this.product),
      isLoading: isLoading ?? this.isLoading,
      error: error,
    );
  }
}

class ProductDetailNotifier extends Notifier<ProductDetailState> {
  int _requestId = 0;

  @override
  ProductDetailState build() => const ProductDetailState();

  Future<void> loadProduct(String id) async {
    final requestId = ++_requestId;
    state = state.copyWith(isLoading: true, error: null, clearProduct: true);

    try {
      final dio = ref.read(apiClientProvider);
      final response = await dio.get<Map<String, dynamic>>('/api/v1/products/$id');
      if (requestId != _requestId) {
        return;
      }
      state = state.copyWith(isLoading: false, product: ProductDetail.fromJson(response.data!));
    } catch (e) {
      if (requestId != _requestId) {
        return;
      }
      state = state.copyWith(
        isLoading: false,
        error: 'Ce produit est introuvable ou n\'est plus disponible.',
      );
    }
  }
}

final productDetailProvider = NotifierProvider<ProductDetailNotifier, ProductDetailState>(ProductDetailNotifier.new);
