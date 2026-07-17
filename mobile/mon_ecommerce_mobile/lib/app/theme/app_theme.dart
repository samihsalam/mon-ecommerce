import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import 'design_tokens.dart';

/// Material 3 theme wiring the Élégance Naturelle tokens (`AppTokens`) and the
/// Cormorant Garamond (H1/H2) + DM Sans (everything else) type duo.
class AppTheme {
  AppTheme._();

  static ThemeData get lightTheme {
    final base = ThemeData(
      useMaterial3: true,
      brightness: Brightness.light,
      colorScheme: ColorScheme.fromSeed(
        seedColor: AppTokens.accentColor,
        brightness: Brightness.light,
        primary: AppTokens.accentColor,
        secondary: AppTokens.accentHoverColor,
        surface: AppTokens.bgColor,
        error: AppTokens.errorColor,
      ),
      scaffoldBackgroundColor: AppTokens.bgColor,
    );

    final bodyTextTheme = GoogleFonts.dmSansTextTheme(base.textTheme).apply(
      bodyColor: AppTokens.textColor,
      displayColor: AppTokens.textColor,
    );

    final textTheme = bodyTextTheme.copyWith(
      headlineLarge: GoogleFonts.cormorantGaramond(
        textStyle: bodyTextTheme.headlineLarge,
        fontWeight: FontWeight.w400,
        color: AppTokens.textColor,
      ),
      headlineMedium: GoogleFonts.cormorantGaramond(
        textStyle: bodyTextTheme.headlineMedium,
        fontWeight: FontWeight.w500,
        color: AppTokens.textColor,
      ),
    );

    return base.copyWith(
      textTheme: textTheme,
      cardTheme: CardThemeData(
        color: AppTokens.bgColor,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppTokens.radiusCard),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppTokens.radiusCard),
          borderSide: const BorderSide(color: AppTokens.borderColor),
        ),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppTokens.radiusButton),
          ),
        ),
      ),
    );
  }
}
