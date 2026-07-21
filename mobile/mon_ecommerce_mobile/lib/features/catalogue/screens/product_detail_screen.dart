import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/theme/design_tokens.dart';
import '../providers/product_detail_provider.dart';

class ProductDetailScreen extends ConsumerStatefulWidget {
  const ProductDetailScreen({super.key, required this.productId});

  final String productId;

  @override
  ConsumerState<ProductDetailScreen> createState() => _ProductDetailScreenState();
}

class _ProductDetailScreenState extends ConsumerState<ProductDetailScreen> {
  final _pageController = PageController();
  int _activeImage = 0;

  @override
  void initState() {
    super.initState();
    // Called directly, not wrapped in Future.microtask — same reasoning established in Story 3.4:
    // the synchronous prefix of loadProduct() (clearing any previous product, setting isLoading)
    // runs before the first build() this way, so the very first frame already shows the loading
    // state instead of whatever the provider last held.
    ref.read(productDetailProvider.notifier).loadProduct(widget.productId);
  }

  @override
  void dispose() {
    _pageController.dispose();
    super.dispose();
  }

  String _formatPrice(int cents) => '${(cents / 100).toStringAsFixed(2)} €';

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(productDetailProvider);

    return Scaffold(
      appBar: AppBar(title: Text(state.product?.name ?? 'Produit')),
      body: _buildBody(state),
    );
  }

  Widget _buildBody(ProductDetailState state) {
    if (state.isLoading) {
      return const Center(child: CircularProgressIndicator());
    }

    if (state.error != null) {
      return Center(child: Text(state.error!, style: const TextStyle(color: AppTokens.errorColor)));
    }

    final product = state.product;
    if (product == null) {
      return const SizedBox.shrink();
    }

    return ListView(
      padding: const EdgeInsets.all(AppTokens.space16),
      children: [
        _buildGallery(product),
        const SizedBox(height: AppTokens.space16),
        Text(product.name, style: const TextStyle(fontSize: 24, fontWeight: FontWeight.w600)),
        const SizedBox(height: AppTokens.space8),
        Text(_formatPrice(product.priceInCents), style: const TextStyle(fontSize: 18)),
        const SizedBox(height: AppTokens.space16),
        const Chip(label: Text('Retour facile 14j')),
        const SizedBox(height: AppTokens.space16),
        Text(product.description),
        const SizedBox(height: AppTokens.space16),
        if (product.material != null) _buildAttribute('Matière', product.material!),
        if (product.color != null) _buildAttribute('Couleur', product.color!),
        if (product.dimensions != null) _buildAttribute('Dimensions', product.dimensions!),
        const SizedBox(height: AppTokens.space16),
        Text(
          product.inStock ? 'En stock' : 'Rupture de stock',
          style: TextStyle(color: product.inStock ? AppTokens.successColor : AppTokens.errorColor),
        ),
      ],
    );
  }

  Widget _buildGallery(ProductDetail product) {
    if (product.imageUrls.isEmpty) {
      return AspectRatio(
        aspectRatio: 3 / 4,
        child: Container(
          color: AppTokens.bgSecondaryColor,
          child: const Center(child: Icon(Icons.image_outlined, color: AppTokens.textSecondaryColor)),
        ),
      );
    }

    return Column(
      children: [
        AspectRatio(
          aspectRatio: 3 / 4,
          child: PageView.builder(
            controller: _pageController,
            itemCount: product.imageUrls.length,
            onPageChanged: (index) => setState(() => _activeImage = index),
            itemBuilder: (context, index) {
              return Semantics(
                label: 'Image ${index + 1} sur ${product.imageUrls.length}',
                image: true,
                child: Image.network(
                  product.imageUrls[index],
                  fit: BoxFit.cover,
                  loadingBuilder: (context, child, progress) =>
                      progress == null ? child : Container(color: AppTokens.bgSecondaryColor),
                  errorBuilder: (context, error, stackTrace) => Container(
                    color: AppTokens.bgSecondaryColor,
                    child: const Icon(Icons.image_not_supported_outlined, color: AppTokens.textSecondaryColor),
                  ),
                ),
              );
            },
          ),
        ),
        if (product.imageUrls.length > 1) ...[
          const SizedBox(height: AppTokens.space8),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              for (var i = 0; i < product.imageUrls.length; i++)
                Container(
                  margin: const EdgeInsets.symmetric(horizontal: 2),
                  width: 8,
                  height: 8,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    color: i == _activeImage ? AppTokens.textColor : AppTokens.borderColor,
                  ),
                ),
            ],
          ),
        ],
      ],
    );
  }

  Widget _buildAttribute(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppTokens.space8),
      child: Row(
        children: [
          Text('$label : ', style: const TextStyle(color: AppTokens.textSecondaryColor)),
          Text(value),
        ],
      ),
    );
  }
}
