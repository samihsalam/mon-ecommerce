import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:mon_ecommerce_mobile/features/auth/screens/register_screen.dart';

void main() {
  Widget buildTestable() {
    return const ProviderScope(
      child: MaterialApp(home: RegisterScreen()),
    );
  }

  testWidgets('shows validation errors when submitting an empty form', (WidgetTester tester) async {
    await tester.pumpWidget(buildTestable());

    await tester.tap(find.widgetWithText(ElevatedButton, 'Créer mon compte'));
    await tester.pump();

    expect(find.text('Le nom est requis.'), findsOneWidget);
    expect(find.text('Veuillez saisir un email.'), findsOneWidget);
    expect(find.text('Le mot de passe doit contenir au moins 8 caractères.'), findsOneWidget);
  });

  testWidgets('shows an invalid-email error for a malformed email', (WidgetTester tester) async {
    await tester.pumpWidget(buildTestable());

    await tester.enterText(find.widgetWithText(TextFormField, 'Email'), 'not-an-email');
    await tester.tap(find.widgetWithText(ElevatedButton, 'Créer mon compte'));
    await tester.pump();

    expect(find.text('Veuillez saisir un email valide.'), findsOneWidget);
  });
}
