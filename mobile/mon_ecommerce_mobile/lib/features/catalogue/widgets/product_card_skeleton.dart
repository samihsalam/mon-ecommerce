import 'package:flutter/material.dart';

import '../../../app/theme/design_tokens.dart';

class ProductCardSkeleton extends StatelessWidget {
  const ProductCardSkeleton({super.key});

  @override
  Widget build(BuildContext context) {
    // excludeSemantics: a loading placeholder has no content a screen reader should announce.
    return Semantics(
      excludeSemantics: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          AspectRatio(
            aspectRatio: 3 / 4,
            child: ClipRRect(
              borderRadius: BorderRadius.circular(AppTokens.radiusCard),
              child: Container(color: AppTokens.bgSecondaryColor),
            ),
          ),
          const SizedBox(height: AppTokens.space8),
          Container(height: 14, width: double.infinity, color: AppTokens.bgSecondaryColor),
          const SizedBox(height: AppTokens.space4),
          Container(height: 14, width: 60, color: AppTokens.bgSecondaryColor),
        ],
      ),
    );
  }
}
