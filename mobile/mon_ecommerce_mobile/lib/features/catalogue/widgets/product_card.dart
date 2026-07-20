import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../app/theme/design_tokens.dart';
import '../providers/catalogue_provider.dart';

class ProductCard extends StatelessWidget {
  const ProductCard({super.key, required this.product});

  final ProductSummary product;

  String _formatPrice() => '${(product.priceInCents / 100).toStringAsFixed(2)} €';

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: '${product.name}, ${_formatPrice()}',
      button: true,
      // Without this, the three child Text widgets (name/price/stock) each keep their own
      // semantics node alongside this explicit label — a screen-reader user hears the label once,
      // then hears the name and price AGAIN as separate nodes while swiping through the card.
      // excludeSemantics folds them all into just this one explicit node, matching what
      // ProductCardSkeleton already does correctly for its own (empty) content.
      excludeSemantics: true,
      child: InkWell(
        // push, not go: `go` REPLACES the current location, which would drop '/catalogue' (and its
        // categoryId) from the navigation stack — breaking AC #6's "filter state persists when
        // navigating back from product detail" once Story 3.5 actually builds this destination.
        // `push` keeps '/catalogue?categoryId=...' on the stack so a real back restores it.
        onTap: () => context.push('/produits/${product.id}'),
        borderRadius: BorderRadius.circular(AppTokens.radiusCard),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            AspectRatio(
              aspectRatio: 3 / 4,
              child: ClipRRect(
                borderRadius: BorderRadius.circular(AppTokens.radiusCard),
                child: product.imageUrl != null
                    ? CachedNetworkImage(
                        imageUrl: product.imageUrl!,
                        fit: BoxFit.cover,
                        placeholder: (context, url) => Container(color: AppTokens.bgSecondaryColor),
                        errorWidget: (context, url, error) => Container(
                          color: AppTokens.bgSecondaryColor,
                          child: const Icon(Icons.image_not_supported_outlined, color: AppTokens.textSecondaryColor),
                        ),
                      )
                    : Container(
                        color: AppTokens.bgSecondaryColor,
                        child: const Icon(Icons.image_outlined, color: AppTokens.textSecondaryColor),
                      ),
              ),
            ),
            const SizedBox(height: AppTokens.space8),
            Text(
              product.name,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(fontWeight: FontWeight.w600, color: AppTokens.textColor),
            ),
            const SizedBox(height: AppTokens.space4),
            Text(_formatPrice(), style: const TextStyle(color: AppTokens.textColor)),
            if (!product.inStock)
              const Text('Rupture de stock', style: TextStyle(color: AppTokens.errorColor, fontSize: 12)),
          ],
        ),
      ),
    );
  }
}
