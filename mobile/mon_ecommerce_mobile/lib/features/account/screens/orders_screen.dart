import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/theme/design_tokens.dart';
import '../providers/orders_provider.dart';

class OrdersScreen extends ConsumerStatefulWidget {
  const OrdersScreen({super.key});

  @override
  ConsumerState<OrdersScreen> createState() => _OrdersScreenState();
}

class _OrdersScreenState extends ConsumerState<OrdersScreen> {
  @override
  void initState() {
    super.initState();
    Future.microtask(() => ref.read(ordersProvider.notifier).loadOrders());
  }

  String _formatAmount(int cents) => '${(cents / 100).toStringAsFixed(2)} €';

  @override
  Widget build(BuildContext context) {
    final ordersState = ref.watch(ordersProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Historique des commandes')),
      body: Padding(
        padding: const EdgeInsets.all(AppTokens.space16),
        child: ordersState.isLoading
            ? const Center(child: CircularProgressIndicator())
            : ordersState.error != null
                ? Text(ordersState.error!, style: const TextStyle(color: AppTokens.errorColor))
                : ordersState.orders.isEmpty
                    ? Center(
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            const Text('Aucune commande pour le moment'),
                            const SizedBox(height: AppTokens.space16),
                            ElevatedButton(
                              onPressed: () => context.go('/'),
                              child: const Text('Commencer à shopper'),
                            ),
                          ],
                        ),
                      )
                    : ListView.separated(
                        itemCount: ordersState.orders.length,
                        separatorBuilder: (context, index) => const SizedBox(height: AppTokens.space16),
                        itemBuilder: (context, index) {
                          final order = ordersState.orders[index];
                          return ListTile(
                            onTap: () => context.go('/compte/commandes/${order.id}'),
                            title: Text(order.orderNumber),
                            subtitle: Text('${order.date} · ${order.status}'),
                            trailing: Text(_formatAmount(order.totalInCents)),
                          );
                        },
                      ),
      ),
    );
  }
}
