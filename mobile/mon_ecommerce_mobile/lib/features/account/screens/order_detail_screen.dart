import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/theme/design_tokens.dart';
import '../providers/orders_provider.dart';

class OrderDetailScreen extends ConsumerStatefulWidget {
  const OrderDetailScreen({super.key, required this.orderId});

  final String orderId;

  @override
  ConsumerState<OrderDetailScreen> createState() => _OrderDetailScreenState();
}

class _OrderDetailScreenState extends ConsumerState<OrderDetailScreen> {
  @override
  void initState() {
    super.initState();
    // Deliberately not wrapped in Future.microtask: loadOrderDetail's synchronous prefix (before
    // its first `await`) clears selectedOrder and sets isLoading — calling it directly here
    // means that happens before this widget's first build(). With a microtask wrapper, that
    // clearing was deferred past the first frame, so navigating from one order's detail straight
    // to another's briefly rendered the FIRST order's stale data (title, items, address) before
    // flipping to the loading spinner.
    ref.read(ordersProvider.notifier).loadOrderDetail(widget.orderId);
  }

  String _formatAmount(int cents) => '${(cents / 100).toStringAsFixed(2)} €';

  @override
  Widget build(BuildContext context) {
    final ordersState = ref.watch(ordersProvider);
    final order = ordersState.selectedOrder;

    return Scaffold(
      appBar: AppBar(title: Text(order?.orderNumber ?? 'Détail de la commande')),
      body: Padding(
        padding: const EdgeInsets.all(AppTokens.space16),
        child: ordersState.isLoading
            ? const Center(child: CircularProgressIndicator())
            : ordersState.error != null
                ? Text(ordersState.error!, style: const TextStyle(color: AppTokens.errorColor))
                : order == null
                    ? const SizedBox.shrink()
                    : ListView(
                        children: [
                          Text(order.status, style: const TextStyle(fontWeight: FontWeight.bold)),
                          const SizedBox(height: AppTokens.space24),
                          const Text('Articles', style: TextStyle(fontWeight: FontWeight.bold)),
                          const SizedBox(height: AppTokens.space16),
                          for (final item in order.items)
                            Padding(
                              padding: const EdgeInsets.only(bottom: AppTokens.space16),
                              child: Row(
                                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                children: [
                                  Text('${item.productName} × ${item.quantity}'),
                                  Text(_formatAmount(item.unitPriceInCents * item.quantity)),
                                ],
                              ),
                            ),
                          Align(
                            alignment: Alignment.centerRight,
                            child: Text(
                              'Total : ${_formatAmount(order.totalInCents)}',
                              style: const TextStyle(fontWeight: FontWeight.bold),
                            ),
                          ),
                          const SizedBox(height: AppTokens.space24),
                          const Text('Adresse de livraison', style: TextStyle(fontWeight: FontWeight.bold)),
                          const SizedBox(height: AppTokens.space16),
                          Text(
                            '${order.shippingAddress.street}, ${order.shippingAddress.postalCode} '
                            '${order.shippingAddress.city}, ${order.shippingAddress.country}',
                          ),
                          if (order.trackingNumber != null) ...[
                            const SizedBox(height: AppTokens.space24),
                            const Text('Suivi', style: TextStyle(fontWeight: FontWeight.bold)),
                            const SizedBox(height: AppTokens.space16),
                            Text(order.trackingNumber!),
                          ],
                        ],
                      ),
      ),
    );
  }
}
