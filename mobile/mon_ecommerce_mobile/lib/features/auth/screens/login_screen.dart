import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/theme/design_tokens.dart';
import '../providers/auth_provider.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  static final _emailPattern = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');

  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _onSubmit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final success = await ref.read(authProvider.notifier).login(
          _emailController.text,
          _passwordController.text,
        );

    if (success && mounted) {
      final returnUrl = GoRouterState.of(context).uri.queryParameters['returnUrl'];
      context.go(returnUrl ?? '/');
    }
  }

  @override
  Widget build(BuildContext context) {
    final authState = ref.watch(authProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Se connecter')),
      body: Padding(
        padding: const EdgeInsets.all(AppTokens.space16),
        child: Form(
          key: _formKey,
          autovalidateMode: AutovalidateMode.onUserInteraction,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              TextFormField(
                controller: _emailController,
                decoration: const InputDecoration(labelText: 'Email'),
                keyboardType: TextInputType.emailAddress,
                validator: (value) {
                  if (value == null || value.isEmpty) return 'Veuillez saisir un email.';
                  if (!_emailPattern.hasMatch(value)) return 'Veuillez saisir un email valide.';
                  return null;
                },
              ),
              const SizedBox(height: AppTokens.space16),
              TextFormField(
                controller: _passwordController,
                decoration: const InputDecoration(labelText: 'Mot de passe'),
                obscureText: true,
                validator: (value) => (value == null || value.isEmpty) ? 'Le mot de passe est requis.' : null,
              ),
              Align(
                alignment: Alignment.centerRight,
                child: TextButton(
                  onPressed: () => context.go('/mot-de-passe-oublie'),
                  child: const Text('Mot de passe oublié ?'),
                ),
              ),
              if (authState.error != null) ...[
                const SizedBox(height: AppTokens.space16),
                Text(
                  authState.error!,
                  style: const TextStyle(color: AppTokens.errorColor),
                ),
              ],
              const SizedBox(height: AppTokens.space24),
              ElevatedButton(
                onPressed: authState.isLoading ? null : _onSubmit,
                child: Text(authState.isLoading ? 'Connexion en cours…' : 'Se connecter'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
