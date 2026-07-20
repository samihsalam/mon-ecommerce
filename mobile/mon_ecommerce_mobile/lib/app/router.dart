import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../features/account/screens/order_detail_screen.dart';
import '../features/account/screens/orders_screen.dart';
import '../features/account/screens/profile_screen.dart';
import '../features/auth/providers/auth_provider.dart';
import '../features/auth/screens/forgot_password_screen.dart';
import '../features/auth/screens/login_screen.dart';
import '../features/auth/screens/register_screen.dart';
import '../features/auth/screens/reset_password_screen.dart';
import '../features/catalogue/screens/search_screen.dart';
import '../shared/services/secure_storage.dart';

final router = GoRouter(
  initialLocation: '/',
  // AuthNotifier's isAuthenticated is set asynchronously (Story 2.2's cold-start check) and
  // go_router's redirect isn't naturally reactive to Riverpod state changes without
  // refreshListenable ChangeNotifier bridging. Reading secure storage directly here is simpler
  // and avoids that plumbing — revisit with proper refreshListenable wiring if this protected-
  // path list keeps growing.
  redirect: (context, state) async {
    // Exact match or a real path-segment boundary (trailing '/') — not a raw string prefix.
    // `startsWith('/compte/commandes')` alone would also match a hypothetical future route like
    // `/compte/commandes-publiques` that isn't meant to be protected.
    final location = state.matchedLocation;
    final isProtected = location == '/compte' || location == '/compte/commandes' || location.startsWith('/compte/commandes/');
    if (!isProtected) {
      return null;
    }

    final container = ProviderScope.containerOf(context, listen: false);

    String? token;
    try {
      token = await container.read(secureStorageProvider).accessToken;
    } catch (e) {
      // Fail closed to "not authenticated" — a secure-storage read failure (e.g. keystore not
      // yet unlocked on some Android devices right after install/reboot) must not propagate
      // out of redirect and leave navigation in a broken state.
      token = null;
    }

    return token == null ? '/connexion?returnUrl=${Uri.encodeComponent(state.matchedLocation)}' : null;
  },
  routes: [
    GoRoute(path: '/', builder: (context, state) => const _HomeScreen()),
    GoRoute(path: '/recherche', builder: (context, state) => const SearchScreen()),
    GoRoute(path: '/inscription', builder: (context, state) => const RegisterScreen()),
    GoRoute(path: '/connexion', builder: (context, state) => const LoginScreen()),
    GoRoute(path: '/mot-de-passe-oublie', builder: (context, state) => const ForgotPasswordScreen()),
    // Real-world entry to this screen is mostly via the Angular web email link today — no
    // deep-linking (universal/app links) is configured in this project yet. The screen and
    // go_router query-param wiring are still fully implemented for parity with Angular web.
    GoRoute(
      path: '/reinitialiser-mot-de-passe',
      builder: (context, state) => ResetPasswordScreen(
        email: state.uri.queryParameters['email'] ?? '',
        token: state.uri.queryParameters['token'] ?? '',
      ),
    ),
    GoRoute(path: '/compte', builder: (context, state) => const ProfileScreen()),
    GoRoute(path: '/compte/commandes', builder: (context, state) => const OrdersScreen()),
    GoRoute(
      path: '/compte/commandes/:orderId',
      builder: (context, state) => OrderDetailScreen(orderId: state.pathParameters['orderId']!),
    ),
  ],
);

// Placeholder home — the real catalogue screen lands in Epic 3.
class _HomeScreen extends ConsumerWidget {
  const _HomeScreen();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final authState = ref.watch(authProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('MonEcommerce'),
        actions: [
          IconButton(
            icon: const Icon(Icons.search),
            tooltip: 'Rechercher',
            onPressed: () => context.go('/recherche'),
          ),
        ],
      ),
      body: Center(
        child: authState.isAuthenticated
            ? Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  ElevatedButton(
                    onPressed: () => context.go('/compte'),
                    child: const Text('Mon compte'),
                  ),
                  TextButton(
                    onPressed: () => ref.read(authProvider.notifier).logout(),
                    child: const Text('Se déconnecter'),
                  ),
                ],
              )
            : Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  ElevatedButton(
                    onPressed: () => context.go('/inscription'),
                    child: const Text('Créer un compte'),
                  ),
                  TextButton(
                    onPressed: () => context.go('/connexion'),
                    child: const Text('Se connecter'),
                  ),
                ],
              ),
      ),
    );
  }
}
