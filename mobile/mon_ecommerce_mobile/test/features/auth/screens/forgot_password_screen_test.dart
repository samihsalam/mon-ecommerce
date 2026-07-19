import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:mon_ecommerce_mobile/features/auth/screens/forgot_password_screen.dart';

void main() {
  Widget buildTestable() {
    return const ProviderScope(
      child: MaterialApp(home: ForgotPasswordScreen()),
    );
  }

  testWidgets('shows a validation error when submitting an empty form', (WidgetTester tester) async {
    await tester.pumpWidget(buildTestable());

    await tester.tap(find.widgetWithText(ElevatedButton, 'Envoyer le lien de réinitialisation'));
    await tester.pump();

    expect(find.text('Veuillez saisir un email.'), findsOneWidget);
  });

  testWidgets('shows an invalid-email error for a malformed email', (WidgetTester tester) async {
    await tester.pumpWidget(buildTestable());

    await tester.enterText(find.widgetWithText(TextFormField, 'Email'), 'not-an-email');
    await tester.tap(find.widgetWithText(ElevatedButton, 'Envoyer le lien de réinitialisation'));
    await tester.pump();

    expect(find.text('Veuillez saisir un email valide.'), findsOneWidget);
  });
}
