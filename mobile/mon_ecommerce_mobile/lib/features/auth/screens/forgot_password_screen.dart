import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/theme/design_tokens.dart';
import '../providers/auth_provider.dart';

class ForgotPasswordScreen extends ConsumerStatefulWidget {
  const ForgotPasswordScreen({super.key});

  @override
  ConsumerState<ForgotPasswordScreen> createState() => _ForgotPasswordScreenState();
}

class _ForgotPasswordScreenState extends ConsumerState<ForgotPasswordScreen> {
  static final _emailPattern = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');

  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();

  // Shown after any submit attempt that reaches the backend, regardless of whether the
  // email is actually registered — the backend never reveals that, and neither should the UI.
  bool _submitted = false;

  @override
  void dispose() {
    _emailController.dispose();
    super.dispose();
  }

  Future<void> _onSubmit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final success = await ref.read(authProvider.notifier).forgotPassword(_emailController.text);

    if (success && mounted) {
      setState(() => _submitted = true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final authState = ref.watch(authProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Mot de passe oublié')),
      body: Padding(
        padding: const EdgeInsets.all(AppTokens.space16),
        child: _submitted
            ? const Text(
                'Si un compte existe avec cet email, vous recevrez un lien de réinitialisation sous peu.',
              )
            : Form(
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
                      child: Text(authState.isLoading ? 'Envoi en cours…' : 'Envoyer le lien de réinitialisation'),
                    ),
                  ],
                ),
              ),
      ),
    );
  }
}
