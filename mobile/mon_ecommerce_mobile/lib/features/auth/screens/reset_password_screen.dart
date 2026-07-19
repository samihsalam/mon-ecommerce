import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/theme/design_tokens.dart';
import '../providers/auth_provider.dart';

class ResetPasswordScreen extends ConsumerStatefulWidget {
  const ResetPasswordScreen({super.key, required this.email, required this.token});

  final String email;
  final String token;

  @override
  ConsumerState<ResetPasswordScreen> createState() => _ResetPasswordScreenState();
}

class _ResetPasswordScreenState extends ConsumerState<ResetPasswordScreen> {
  final _formKey = GlobalKey<FormState>();
  final _newPasswordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();

  @override
  void dispose() {
    _newPasswordController.dispose();
    _confirmPasswordController.dispose();
    super.dispose();
  }

  Future<void> _onSubmit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final success = await ref.read(authProvider.notifier).resetPassword(
          widget.email,
          widget.token,
          _newPasswordController.text,
        );

    if (success && mounted) {
      context.go('/connexion');
    }
  }

  @override
  Widget build(BuildContext context) {
    final authState = ref.watch(authProvider);

    // A direct/stale/stripped visit to this route without email+token would otherwise submit
    // empty strings, fail backend validation with a 422, and confusingly show a generic error
    // instead of a clear "this link is broken" signal.
    if (widget.email.isEmpty || widget.token.isEmpty) {
      return Scaffold(
        appBar: AppBar(title: const Text('Réinitialiser le mot de passe')),
        body: Padding(
          padding: const EdgeInsets.all(AppTokens.space16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text(
                'Ce lien de réinitialisation est invalide ou incomplet.',
                style: TextStyle(color: AppTokens.errorColor),
              ),
              const SizedBox(height: AppTokens.space16),
              TextButton(
                onPressed: () => context.go('/mot-de-passe-oublie'),
                child: const Text('Demander un nouveau lien'),
              ),
            ],
          ),
        ),
      );
    }

    return Scaffold(
      appBar: AppBar(title: const Text('Réinitialiser le mot de passe')),
      body: Padding(
        padding: const EdgeInsets.all(AppTokens.space16),
        child: Form(
          key: _formKey,
          autovalidateMode: AutovalidateMode.onUserInteraction,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              TextFormField(
                controller: _newPasswordController,
                decoration: const InputDecoration(labelText: 'Nouveau mot de passe'),
                obscureText: true,
                validator: (value) => (value == null || value.length < 8)
                    ? 'Le mot de passe doit contenir au moins 8 caractères.'
                    : null,
              ),
              const SizedBox(height: AppTokens.space16),
              TextFormField(
                controller: _confirmPasswordController,
                decoration: const InputDecoration(labelText: 'Confirmer le mot de passe'),
                obscureText: true,
                validator: (value) =>
                    value != _newPasswordController.text ? 'Les mots de passe ne correspondent pas.' : null,
              ),
              if (authState.error != null) ...[
                const SizedBox(height: AppTokens.space16),
                Text(
                  authState.error!,
                  style: const TextStyle(color: AppTokens.errorColor),
                ),
                TextButton(
                  onPressed: () => context.go('/mot-de-passe-oublie'),
                  child: const Text('Demander un nouveau lien'),
                ),
              ],
              const SizedBox(height: AppTokens.space24),
              ElevatedButton(
                onPressed: authState.isLoading ? null : _onSubmit,
                child: Text(authState.isLoading ? 'Réinitialisation en cours…' : 'Réinitialiser le mot de passe'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
