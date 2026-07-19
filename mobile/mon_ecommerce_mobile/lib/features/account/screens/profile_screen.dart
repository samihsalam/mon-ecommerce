import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/theme/design_tokens.dart';
import '../providers/account_provider.dart';

class ProfileScreen extends ConsumerStatefulWidget {
  const ProfileScreen({super.key});

  @override
  ConsumerState<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends ConsumerState<ProfileScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _emailController = TextEditingController();
  final _currentPasswordController = TextEditingController();

  String _loadedEmail = '';
  bool _initialized = false;

  @override
  void initState() {
    super.initState();
    Future.microtask(() async {
      await ref.read(accountProvider.notifier).loadProfile();
      final profile = ref.read(accountProvider).profile;
      if (profile != null && mounted) {
        setState(() {
          _nameController.text = profile.name;
          _emailController.text = profile.email;
          _loadedEmail = profile.email;
          _initialized = true;
        });
      }
    });
  }

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    _currentPasswordController.dispose();
    super.dispose();
  }

  bool get _isEmailChanged => _emailController.text != _loadedEmail;

  Future<void> _onSubmit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final success = await ref.read(accountProvider.notifier).updateProfile(
          _nameController.text,
          _emailController.text,
          _isEmailChanged ? _currentPasswordController.text : null,
        );

    if (success && mounted) {
      setState(() {
        _loadedEmail = _emailController.text;
        _currentPasswordController.clear();
      });
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Profil mis à jour')));
    }
  }

  @override
  Widget build(BuildContext context) {
    final accountState = ref.watch(accountProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Mon profil')),
      body: Padding(
        padding: const EdgeInsets.all(AppTokens.space16),
        child: !_initialized
            ? const Center(child: CircularProgressIndicator())
            : Form(
                key: _formKey,
                autovalidateMode: AutovalidateMode.onUserInteraction,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Align(
                      alignment: Alignment.centerLeft,
                      child: TextButton(
                        onPressed: () => context.go('/compte/commandes'),
                        child: const Text('Historique des commandes'),
                      ),
                    ),
                    const SizedBox(height: AppTokens.space16),
                    TextFormField(
                      controller: _nameController,
                      decoration: const InputDecoration(labelText: 'Nom'),
                      validator: (value) => (value == null || value.isEmpty) ? 'Le nom est requis.' : null,
                      onChanged: (_) => setState(() {}),
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
                      onChanged: (_) => setState(() {}),
                    ),
                    if (_isEmailChanged) ...[
                      const SizedBox(height: AppTokens.space16),
                      TextFormField(
                        controller: _currentPasswordController,
                        decoration:
                            const InputDecoration(labelText: "Mot de passe actuel (requis pour changer d'email)"),
                        obscureText: true,
                      ),
                    ],
                    if (accountState.error != null) ...[
                      const SizedBox(height: AppTokens.space16),
                      Text(
                        accountState.error!,
                        style: const TextStyle(color: AppTokens.errorColor),
                      ),
                    ],
                    const SizedBox(height: AppTokens.space24),
                    ElevatedButton(
                      onPressed: accountState.isLoading ? null : _onSubmit,
                      child: Text(accountState.isLoading ? 'Enregistrement en cours…' : 'Enregistrer'),
                    ),
                    if (accountState.profile?.addresses.isNotEmpty ?? false) ...[
                      const SizedBox(height: AppTokens.space24),
                      const Text('Adresses enregistrées', style: TextStyle(fontWeight: FontWeight.bold)),
                      const SizedBox(height: AppTokens.space16),
                      for (final address in accountState.profile!.addresses)
                        Padding(
                          padding: const EdgeInsets.only(bottom: AppTokens.space16),
                          child: Text('${address.street}, ${address.postalCode} ${address.city}, ${address.country}'),
                        ),
                    ],
                  ],
                ),
              ),
      ),
    );
  }
}
