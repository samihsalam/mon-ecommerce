import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../shared/services/api_client.dart';
import '../../../shared/services/secure_storage.dart';

class AuthState {
  const AuthState({this.isLoading = false, this.error});

  final bool isLoading;
  final String? error;

  AuthState copyWith({bool? isLoading, String? error}) {
    return AuthState(isLoading: isLoading ?? this.isLoading, error: error);
  }
}

class AuthNotifier extends Notifier<AuthState> {
  @override
  AuthState build() => const AuthState();

  Future<bool> register(String name, String email, String password) async {
    state = state.copyWith(isLoading: true, error: null);

    try {
      final dio = ref.read(apiClientProvider);
      final response = await dio.post<Map<String, dynamic>>(
        '/api/v1/auth/register',
        data: {'name': name, 'email': email, 'password': password},
      );

      final data = response.data!;
      await ref.read(secureStorageProvider).saveTokens(
            accessToken: data['accessToken'] as String,
            refreshToken: data['refreshToken'] as String,
          );

      state = state.copyWith(isLoading: false);
      return true;
    } on DioException catch (e) {
      final message = e.response?.statusCode == 409
          ? 'Un compte existe déjà avec cet email.'
          : "Une erreur est survenue lors de l'inscription. Veuillez réessayer.";
      state = state.copyWith(isLoading: false, error: message);
      return false;
    } catch (e) {
      // Covers non-Dio failures too (e.g. secureStorageProvider.saveTokens throwing
      // a PlatformException) — without this, isLoading would stay stuck true.
      state = state.copyWith(
        isLoading: false,
        error: "Une erreur est survenue lors de l'inscription. Veuillez réessayer.",
      );
      return false;
    }
  }
}

final authProvider = NotifierProvider<AuthNotifier, AuthState>(AuthNotifier.new);
