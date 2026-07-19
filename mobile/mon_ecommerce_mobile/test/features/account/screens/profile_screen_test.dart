import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:mon_ecommerce_mobile/features/account/providers/account_provider.dart';
import 'package:mon_ecommerce_mobile/features/account/screens/profile_screen.dart';

class _FakeAccountNotifier extends AccountNotifier {
  @override
  AccountState build() => const AccountState(
        profile: Profile(
          name: 'Alice',
          email: 'alice@example.com',
          addresses: [],
        ),
      );

  @override
  Future<void> loadProfile() async {
    // No-op: the fake already starts with a loaded profile, and this test suite has no
    // backend to call — avoids the widget test hanging on a real Dio request.
  }
}

void main() {
  Widget buildTestable() {
    return ProviderScope(
      overrides: [accountProvider.overrideWith(_FakeAccountNotifier.new)],
      child: const MaterialApp(home: ProfileScreen()),
    );
  }

  testWidgets('shows the loaded profile in the form fields', (WidgetTester tester) async {
    await tester.pumpWidget(buildTestable());
    await tester.pumpAndSettle();

    expect(find.widgetWithText(TextFormField, 'Nom'), findsOneWidget);
    final nameField = tester.widget<TextFormField>(find.widgetWithText(TextFormField, 'Nom'));
    expect(nameField.controller?.text, 'Alice');
  });

  testWidgets('shows the current-password field only after the email changes', (WidgetTester tester) async {
    await tester.pumpWidget(buildTestable());
    await tester.pumpAndSettle();

    expect(find.text("Mot de passe actuel (requis pour changer d'email)"), findsNothing);

    await tester.enterText(find.widgetWithText(TextFormField, 'Email'), 'alice-new@example.com');
    await tester.pump();

    expect(find.text("Mot de passe actuel (requis pour changer d'email)"), findsOneWidget);
  });
}
