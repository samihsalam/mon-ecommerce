import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:google_fonts/google_fonts.dart';

import 'package:mon_ecommerce_mobile/app/theme/app_theme.dart';
import 'package:mon_ecommerce_mobile/app/theme/design_tokens.dart';

void main() {
  // google_fonts fetches font files over the network by default; disable that in
  // tests so `flutter test` never depends on connectivity (falls back to a
  // bundled system font instead of failing/hanging offline or in CI).
  setUpAll(() {
    GoogleFonts.config.allowRuntimeFetching = false;
  });

  testWidgets('AppTheme wires AppTokens.accentColor as the primary color', (
    WidgetTester tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.lightTheme,
        home: const Scaffold(body: Text('probe')),
      ),
    );

    final BuildContext context = tester.element(find.text('probe'));
    final ThemeData theme = Theme.of(context);

    expect(theme.colorScheme.primary, AppTokens.accentColor);
  });

  testWidgets('AppTheme applies Cormorant Garamond to headlineLarge (H1)', (
    WidgetTester tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.lightTheme,
        home: const Scaffold(body: Text('probe')),
      ),
    );

    final BuildContext context = tester.element(find.text('probe'));
    final ThemeData theme = Theme.of(context);

    expect(theme.textTheme.headlineLarge?.fontFamily, contains('Cormorant'));
  });

  testWidgets('AppTheme applies DM Sans to bodyLarge', (
    WidgetTester tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.lightTheme,
        home: const Scaffold(body: Text('probe')),
      ),
    );

    final BuildContext context = tester.element(find.text('probe'));
    final ThemeData theme = Theme.of(context);

    expect(theme.textTheme.bodyLarge?.fontFamily, contains('DM Sans'));
  });

  test('AppTokens.radiusCard and radiusButton match the design spec', () {
    expect(AppTokens.radiusCard, 4);
    expect(AppTokens.radiusButton, 2);
  });
}
