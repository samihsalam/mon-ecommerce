import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../features/auth/screens/register_screen.dart';

final router = GoRouter(
  initialLocation: '/',
  routes: [
    GoRoute(path: '/', builder: (context, state) => const _HomeScreen()),
    GoRoute(path: '/inscription', builder: (context, state) => const RegisterScreen()),
  ],
);

// Placeholder home — the real catalogue screen lands in Epic 3.
class _HomeScreen extends StatelessWidget {
  const _HomeScreen();

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('MonEcommerce')),
      body: Center(
        child: ElevatedButton(
          onPressed: () => context.go('/inscription'),
          child: const Text('Créer un compte'),
        ),
      ),
    );
  }
}
