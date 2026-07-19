import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../shared/services/api_client.dart';

class Address {
  const Address({
    required this.id,
    required this.street,
    required this.city,
    required this.postalCode,
    required this.country,
  });

  final String id;
  final String street;
  final String city;
  final String postalCode;
  final String country;

  factory Address.fromJson(Map<String, dynamic> json) => Address(
        id: json['id'] as String,
        street: json['street'] as String,
        city: json['city'] as String,
        postalCode: json['postalCode'] as String,
        country: json['country'] as String,
      );
}

class OrderSummary {
  const OrderSummary({
    required this.id,
    required this.orderNumber,
    required this.date,
    required this.totalInCents,
    required this.status,
  });

  final String id;
  final String orderNumber;
  final String date;
  final int totalInCents;
  final String status;

  factory OrderSummary.fromJson(Map<String, dynamic> json) => OrderSummary(
        id: json['id'] as String,
        orderNumber: json['orderNumber'] as String,
        date: json['date'] as String,
        totalInCents: json['totalInCents'] as int,
        status: json['status'] as String,
      );
}

class OrderItem {
  const OrderItem({required this.productName, required this.unitPriceInCents, required this.quantity});

  final String productName;
  final int unitPriceInCents;
  final int quantity;

  factory OrderItem.fromJson(Map<String, dynamic> json) => OrderItem(
        productName: json['productName'] as String,
        unitPriceInCents: json['unitPriceInCents'] as int,
        quantity: json['quantity'] as int,
      );
}

class OrderDetail extends OrderSummary {
  const OrderDetail({
    required super.id,
    required super.orderNumber,
    required super.date,
    required super.totalInCents,
    required super.status,
    required this.trackingNumber,
    required this.shippingAddress,
    required this.items,
  });

  final String? trackingNumber;
  final Address shippingAddress;
  final List<OrderItem> items;

  factory OrderDetail.fromJson(Map<String, dynamic> json) => OrderDetail(
        id: json['id'] as String,
        orderNumber: json['orderNumber'] as String,
        date: json['date'] as String,
        totalInCents: json['totalInCents'] as int,
        status: json['status'] as String,
        trackingNumber: json['trackingNumber'] as String?,
        shippingAddress: Address.fromJson(json['shippingAddress'] as Map<String, dynamic>),
        items: (json['items'] as List<dynamic>).map((i) => OrderItem.fromJson(i as Map<String, dynamic>)).toList(),
      );
}

class OrdersState {
  const OrdersState({
    this.orders = const [],
    this.totalCount = 0,
    this.page = 1,
    this.selectedOrder,
    this.isLoading = false,
    this.error,
  });

  final List<OrderSummary> orders;
  final int totalCount;
  final int page;
  final OrderDetail? selectedOrder;
  final bool isLoading;
  final String? error;

  // selectedOrder is deliberately NOT "sticky" like orders/totalCount/page (i.e. no `??
  // this.selectedOrder` fallback) — it's always overwritten with whatever is passed, same as
  // `error`. This lets loadOrderDetail() explicitly clear it to null before fetching a new
  // order; the sticky pattern previously meant a second order's detail screen would render the
  // FIRST order's stale data for one frame (until the fetch resolved), since `initState`'s
  // `Future.microtask` doesn't run before the widget's first `build()`.
  OrdersState copyWith({
    List<OrderSummary>? orders,
    int? totalCount,
    int? page,
    OrderDetail? selectedOrder,
    bool? isLoading,
    String? error,
  }) {
    return OrdersState(
      orders: orders ?? this.orders,
      totalCount: totalCount ?? this.totalCount,
      page: page ?? this.page,
      selectedOrder: selectedOrder,
      isLoading: isLoading ?? this.isLoading,
      error: error,
    );
  }
}

class OrdersNotifier extends Notifier<OrdersState> {
  @override
  OrdersState build() => const OrdersState();

  Future<void> loadOrders({int page = 1}) async {
    state = state.copyWith(isLoading: true, error: null);

    try {
      final dio = ref.read(apiClientProvider);
      final response = await dio.get<Map<String, dynamic>>(
        '/api/v1/account/orders',
        queryParameters: {'page': page, 'pageSize': 10},
      );
      final data = response.data!;
      final orders =
          (data['items'] as List<dynamic>).map((o) => OrderSummary.fromJson(o as Map<String, dynamic>)).toList();
      state = state.copyWith(isLoading: false, orders: orders, totalCount: data['totalCount'] as int, page: page);
    } catch (e) {
      state = state.copyWith(isLoading: false, error: 'Impossible de charger vos commandes. Veuillez réessayer.');
    }
  }

  Future<void> loadOrderDetail(String orderId) async {
    state = state.copyWith(isLoading: true, error: null);

    try {
      final dio = ref.read(apiClientProvider);
      final response = await dio.get<Map<String, dynamic>>('/api/v1/account/orders/$orderId');
      state = state.copyWith(isLoading: false, selectedOrder: OrderDetail.fromJson(response.data!));
    } catch (e) {
      state = state.copyWith(isLoading: false, error: 'Impossible de charger cette commande. Veuillez réessayer.');
    }
  }
}

final ordersProvider = NotifierProvider<OrdersNotifier, OrdersState>(OrdersNotifier.new);
