import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/theme/design_tokens.dart';
import '../providers/auth_provider.dart';

class RegisterScreen extends ConsumerStatefulWidget {
  const RegisterScreen({super.key});

  @override
  ConsumerState<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends ConsumerState<RegisterScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _onSubmit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final success = await ref.read(authProvider.notifier).register(
          _nameController.text,
          _emailController.text,
          _passwordController.text,
        );

    if (success && mounted) {
      context.go('/');
    }
  }

  @override
  Widget build(BuildContext context) {
    final authState = ref.watch(authProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Créer un compte')),
      body: Padding(
        padding: const EdgeInsets.all(AppTokens.space16),
        child: Form(
          key: _formKey,
          autovalidateMode: AutovalidateMode.onUserInteraction,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              TextFormField(
                controller: _nameController,
                decoration: const InputDecoration(labelText: 'Nom'),
                validator: (value) => (value == null || value.isEmpty) ? 'Le nom est requis.' : null,
              ),
              const SizedBox(height: AppTokens.space16),
              TextFormField(
                controller: _emailController,
                decoration: const InputDecoration(labelText: 'Email'),
                keyboardType: TextInputType.emailAddress,
                validator: (value) {
                  if (value == null || value.isEmpty) return 'Veuillez saisir un email.';
                  if (!value.contains('@')) return 'Veuillez saisir un email valide.';
                  return null;
                },
              ),
              const SizedBox(height: AppTokens.space16),
              TextFormField(
                controller: _passwordController,
                decoration: const InputDecoration(labelText: 'Mot de passe'),
                obscureText: true,
                validator: (value) => (value == null || value.length < 8)
                    ? 'Le mot de passe doit contenir au moins 8 caractères.'
                    : null,
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
                child: Text(authState.isLoading ? 'Création en cours…' : 'Créer mon compte'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
