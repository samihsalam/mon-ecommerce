import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'package:mon_ecommerce_mobile/features/account/providers/orders_provider.dart';
import 'package:mon_ecommerce_mobile/features/account/screens/orders_screen.dart';

class _FakeEmptyOrdersNotifier extends OrdersNotifier {
  @override
  OrdersState build() => const OrdersState(orders: [], totalCount: 0);

  @override
  Future<void> loadOrders({int page = 1}) async {}
}

class _FakeOrdersNotifier extends OrdersNotifier {
  @override
  OrdersState build() => const OrdersState(
        orders: [
          OrderSummary(id: 'order-1', orderNumber: '#ABCD1234', date: '2026-01-01', totalInCents: 1000, status: 'Expédiée'),
        ],
        totalCount: 1,
      );

  @override
  Future<void> loadOrders({int page = 1}) async {}
}

void main() {
  Widget buildTestable(OrdersNotifier Function() notifier) {
    final router = GoRouter(routes: [
      GoRoute(path: '/', builder: (context, state) => const OrdersScreen()),
    ]);
    return ProviderScope(
      overrides: [ordersProvider.overrideWith(notifier)],
      child: MaterialApp.router(routerConfig: router),
    );
  }

  testWidgets('shows the empty state with a CTA when there are no orders', (WidgetTester tester) async {
    await tester.pumpWidget(buildTestable(_FakeEmptyOrdersNotifier.new));
    await tester.pumpAndSettle();

    expect(find.text('Aucune commande pour le moment'), findsOneWidget);
    expect(find.text('Commencer à shopper'), findsOneWidget);
  });

  testWidgets('shows the order list when orders exist', (WidgetTester tester) async {
    await tester.pumpWidget(buildTestable(_FakeOrdersNotifier.new));
    await tester.pumpAndSettle();

    expect(find.text('#ABCD1234'), findsOneWidget);
  });
}
