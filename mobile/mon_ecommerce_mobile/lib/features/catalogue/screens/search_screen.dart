import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/theme/design_tokens.dart';
import '../providers/catalogue_provider.dart';

const _minTermLength = 2;
const _suggestionDebounce = Duration(milliseconds: 300);

class SearchScreen extends ConsumerStatefulWidget {
  const SearchScreen({super.key});

  @override
  ConsumerState<SearchScreen> createState() => _SearchScreenState();
}

class _SearchScreenState extends ConsumerState<SearchScreen> {
  final _controller = TextEditingController();
  Timer? _debounce;
  String _submittedTerm = '';
  bool _showSuggestions = false;

  @override
  void initState() {
    super.initState();
    Future.microtask(() => ref.read(catalogueProvider.notifier).loadCategories());
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _controller.dispose();
    super.dispose();
  }

  void _onChanged(String value) {
    _debounce?.cancel();

    final trimmed = value.trim();
    if (trimmed.length < _minTermLength) {
      ref.read(catalogueProvider.notifier).clearSuggestions();
      setState(() => _showSuggestions = false);
      return;
    }

    _debounce = Timer(_suggestionDebounce, () {
      ref.read(catalogueProvider.notifier).loadSuggestions(trimmed);
      setState(() => _showSuggestions = true);
    });
  }

  void _submit(String term) {
    final trimmed = term.trim();
    if (trimmed.length < _minTermLength) {
      return;
    }

    _controller.text = trimmed;
    setState(() {
      _showSuggestions = false;
      _submittedTerm = trimmed;
    });
    ref.read(catalogueProvider.notifier).search(trimmed);
  }

  String _formatAmount(int cents) => '${(cents / 100).toStringAsFixed(2)} €';

  @override
  Widget build(BuildContext context) {
    final catalogueState = ref.watch(catalogueProvider);
    final hasSuggestions =
        catalogueState.suggestions.categories.isNotEmpty || catalogueState.suggestions.products.isNotEmpty;

    return Scaffold(
      appBar: AppBar(
        title: TextField(
          controller: _controller,
          autofocus: true,
          textInputAction: TextInputAction.search,
          decoration: const InputDecoration(hintText: 'Rechercher un produit…', border: InputBorder.none),
          onChanged: _onChanged,
          onSubmitted: _submit,
        ),
      ),
      body: Padding(
        padding: const EdgeInsets.all(AppTokens.space16),
        child: Stack(
          children: [
            _buildResults(catalogueState),
            if (_showSuggestions && hasSuggestions) _buildSuggestions(catalogueState),
          ],
        ),
      ),
    );
  }

  Widget _buildSuggestions(CatalogueState state) {
    return Positioned(
      top: 0,
      left: 0,
      right: 0,
      child: Material(
        elevation: 4,
        borderRadius: BorderRadius.circular(AppTokens.radiusCard),
        child: ListView(
          shrinkWrap: true,
          children: [
            for (final category in state.suggestions.categories)
              ListTile(
                title: Text(category),
                trailing: const Text('catégorie', style: TextStyle(fontSize: 12)),
                onTap: () => _submit(category),
              ),
            for (final product in state.suggestions.products)
              ListTile(title: Text(product), onTap: () => _submit(product)),
          ],
        ),
      ),
    );
  }

  Widget _buildResults(CatalogueState state) {
    if (_submittedTerm.isEmpty) {
      return const Center(child: Text('Saisissez au moins 2 caractères pour lancer une recherche.'));
    }

    if (state.isSearching) {
      return const Center(child: CircularProgressIndicator());
    }

    if (state.searchError != null) {
      return Center(child: Text(state.searchError!, style: const TextStyle(color: AppTokens.errorColor)));
    }

    if (state.results.isEmpty) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text('Aucun résultat pour « $_submittedTerm »'),
            if (state.categories.isNotEmpty) ...[
              const SizedBox(height: AppTokens.space16),
              const Text('Essayez une de ces catégories :'),
              const SizedBox(height: AppTokens.space16),
              Wrap(
                spacing: AppTokens.space8,
                alignment: WrapAlignment.center,
                children: [
                  for (final category in state.categories)
                    ActionChip(
                      label: Text(category.name),
                      onPressed: () => context.go('/?categoryId=${category.id}'),
                    ),
                ],
              ),
            ],
          ],
        ),
      );
    }

    return ListView.separated(
      itemCount: state.results.length,
      separatorBuilder: (context, index) => const SizedBox(height: AppTokens.space16),
      itemBuilder: (context, index) {
        final product = state.results[index];
        return ListTile(title: Text(product.name), trailing: Text(_formatAmount(product.priceInCents)));
      },
    );
  }
}
