import 'package:flutter/material.dart';

/// Élégance Naturelle design tokens — mirrors the Angular web tokens in
/// `frontend/mon-ecommerce-web/src/styles.scss` so both platforms share the
/// same visual values. See `_bmad-output/planning-artifacts/ux-design-specification.md#Fondation Visuelle`.
class AppTokens {
  AppTokens._();

  // Colors
  static const Color bgColor = Color(0xFFFFFFFF);
  static const Color bgSecondaryColor = Color(0xFFFAF8F5);
  static const Color textColor = Color(0xFF111111);
  static const Color textSecondaryColor = Color(0xFF555555);
  static const Color accentColor = Color(0xFFC9A96E);
  static const Color accentHoverColor = Color(0xFFA8864A);
  static const Color borderColor = Color(0xFFE5E5E5);
  static const Color successColor = Color(0xFF6B8F71);
  static const Color errorColor = Color(0xFFC0564A);

  // Spacing — 8px grid (documents the shared scale; use these constants
  // instead of magic numbers when spacing widgets).
  static const double space4 = 4;
  static const double space8 = 8;
  static const double space16 = 16;
  static const double space24 = 24;
  static const double space32 = 32;
  static const double space48 = 48;
  static const double space64 = 64;

  // Border radius
  static const double radiusCard = 4;
  static const double radiusButton = 2;
}
