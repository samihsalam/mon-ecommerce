import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

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
    // addPostFrameCallback, NOT called directly: Riverpod disallows modifying provider state
    // during ANY widget lifecycle method (initState included) — calling this directly crashes
    // with "Tried to modify a provider while the widget tree was building" the moment this screen
    // is actually rendered (confirmed by actually running the app — this path was never exercised
    // by any tooling before). addPostFrameCallback still runs before the user perceives the first
    // frame, so the originally-intended "avoid a flash of the PREVIOUS order's stale data" goal
    // (see history) is still met for practical purposes, just one frame later than assumed.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      ref.read(ordersProvider.notifier).loadOrderDetail(widget.orderId);
    });
  }

  String _formatAmount(int cents) => '${(cents / 100).toStringAsFixed(2)} €';

  // Client-side approximation only (Story 5.1, AC #3) — avoids showing a "Demander un retour"
  // button that would always fail; the backend (using Order.LastModified, not this DTO's `date`)
  // is the actual source of truth for the 14-day window. Mirrors the Angular web page's own
  // isReturnEligible check.
  bool _isReturnEligible(OrderDetail order) {
    final deliveredAt = DateTime.tryParse(order.date);
    if (deliveredAt == null) return false;
    return order.status == 'Livrée' && DateTime.now().difference(deliveredAt) <= const Duration(days: 14);
  }

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
                          const SizedBox(height: AppTokens.space24),
                          const Text('Retour', style: TextStyle(fontWeight: FontWeight.bold)),
                          const SizedBox(height: AppTokens.space16),
                          if (order.returnRequest != null)
                            Text('Demande de retour (${order.returnRequest!.reason}) — statut : ${order.returnRequest!.status}')
                          else if (_isReturnEligible(order))
                            OutlinedButton(
                              onPressed: () => context.go('/compte/commandes/${order.id}/retour'),
                              child: const Text('Demander un retour'),
                            ),
                        ],
                      ),
      ),
    );
  }
}
