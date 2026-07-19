import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:mon_ecommerce_mobile/features/auth/screens/reset_password_screen.dart';

void main() {
  Widget buildTestable() {
    return const ProviderScope(
      child: MaterialApp(home: ResetPasswordScreen(email: 'alice@example.com', token: 'reset-token')),
    );
  }

  testWidgets('shows a validation error when the new password is too short', (WidgetTester tester) async {
    await tester.pumpWidget(buildTestable());

    await tester.enterText(find.widgetWithText(TextFormField, 'Nouveau mot de passe'), 'short');
    await tester.tap(find.widgetWithText(ElevatedButton, 'Réinitialiser le mot de passe'));
    await tester.pump();

    expect(find.text('Le mot de passe doit contenir au moins 8 caractères.'), findsOneWidget);
  });

  testWidgets('shows a mismatch error when the two password fields differ', (WidgetTester tester) async {
    await tester.pumpWidget(buildTestable());

    await tester.enterText(find.widgetWithText(TextFormField, 'Nouveau mot de passe'), 'newpassword1');
    await tester.enterText(find.widgetWithText(TextFormField, 'Confirmer le mot de passe'), 'different1');
    await tester.tap(find.widgetWithText(ElevatedButton, 'Réinitialiser le mot de passe'));
    await tester.pump();

    expect(find.text('Les mots de passe ne correspondent pas.'), findsOneWidget);
  });
}
