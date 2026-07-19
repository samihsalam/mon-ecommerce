import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../features/auth/providers/auth_provider.dart';
import '../features/auth/screens/login_screen.dart';
import '../features/auth/screens/register_screen.dart';

final router = GoRouter(
  initialLocation: '/',
  routes: [
    GoRoute(path: '/', builder: (context, state) => const _HomeScreen()),
    GoRoute(path: '/inscription', builder: (context, state) => const RegisterScreen()),
    GoRoute(path: '/connexion', builder: (context, state) => const LoginScreen()),
  ],
);

// Placeholder home — the real catalogue screen lands in Epic 3.
class _HomeScreen extends ConsumerWidget {
  const _HomeScreen();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final authState = ref.watch(authProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('MonEcommerce')),
      body: Center(
        child: authState.isAuthenticated
            ? ElevatedButton(
                onPressed: () => ref.read(authProvider.notifier).logout(),
                child: const Text('Se déconnecter'),
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
