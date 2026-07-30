import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';

import '../../../app/theme/design_tokens.dart';
import '../providers/orders_provider.dart';

class ReturnRequestScreen extends ConsumerStatefulWidget {
  const ReturnRequestScreen({super.key, required this.orderId});

  final String orderId;

  @override
  ConsumerState<ReturnRequestScreen> createState() => _ReturnRequestScreenState();
}

class _ReturnRequestScreenState extends ConsumerState<ReturnRequestScreen> {
  final _descriptionController = TextEditingController();
  ReturnReason? _reason;
  final List<XFile> _photos = [];
  bool _submitting = false;
  bool _touched = false;

  @override
  void dispose() {
    _descriptionController.dispose();
    super.dispose();
  }

  Future<void> _pickPhotos() async {
    final picked = await ImagePicker().pickMultiImage();
    if (!mounted || picked.isEmpty) return;
    setState(() => _photos.addAll(picked));
  }

  Future<void> _submit() async {
    setState(() => _touched = true);
    if (_reason == null || _descriptionController.text.trim().isEmpty || _submitting) {
      return;
    }

    setState(() => _submitting = true);
    final success = await ref.read(ordersProvider.notifier).requestReturn(
          widget.orderId,
          _reason!,
          _descriptionController.text.trim(),
          _photos.map((f) => f.path).toList(),
        );
    if (!mounted) return;
    setState(() => _submitting = false);

    if (success) {
      context.go('/compte/commandes/${widget.orderId}');
    }
  }

  @override
  Widget build(BuildContext context) {
    final returnError = ref.watch(ordersProvider.select((s) => s.returnError));

    return Scaffold(
      appBar: AppBar(title: const Text('Demander un retour')),
      body: Padding(
        padding: const EdgeInsets.all(AppTokens.space16),
        child: ListView(
          children: [
            DropdownButtonFormField<ReturnReason>(
              initialValue: _reason,
              decoration: InputDecoration(
                labelText: 'Motif',
                errorText: _touched && _reason == null ? 'Le motif est requis.' : null,
              ),
              items: ReturnReason.values
                  .map((r) => DropdownMenuItem(value: r, child: Text(r.label)))
                  .toList(),
              onChanged: (value) => setState(() => _reason = value),
            ),
            const SizedBox(height: AppTokens.space16),
            TextField(
              controller: _descriptionController,
              maxLines: 4,
              decoration: InputDecoration(
                labelText: 'Description',
                errorText: _touched && _descriptionController.text.trim().isEmpty
                    ? 'La description est requise.'
                    : null,
              ),
            ),
            const SizedBox(height: AppTokens.space16),
            OutlinedButton(
              onPressed: _pickPhotos,
              child: Text(_photos.isEmpty ? 'Ajouter des photos (facultatif)' : '${_photos.length} photo(s) sélectionnée(s)'),
            ),
            const SizedBox(height: AppTokens.space16),
            if (returnError != null) ...[
              Text(returnError, style: const TextStyle(color: AppTokens.errorColor)),
              const SizedBox(height: AppTokens.space16),
            ],
            FilledButton(
              onPressed: _submitting ? null : _submit,
              child: Text(_submitting ? 'Envoi en cours…' : 'Envoyer la demande'),
            ),
          ],
        ),
      ),
    );
  }
}
