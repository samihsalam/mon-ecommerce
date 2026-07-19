import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../shared/services/api_client.dart';
import '../../../shared/services/secure_storage.dart';

class AuthState {
  const AuthState({this.isLoading = false, this.error, this.isAuthenticated = false});

  final bool isLoading;
  final String? error;
  final bool isAuthenticated;

  AuthState copyWith({bool? isLoading, String? error, bool? isAuthenticated}) {
    return AuthState(
      isLoading: isLoading ?? this.isLoading,
      error: error,
      isAuthenticated: isAuthenticated ?? this.isAuthenticated,
    );
  }
}

class AuthNotifier extends Notifier<AuthState> {
  @override
  AuthState build() {
    // Not an AsyncNotifier — same reasoning as Story 2.1 (simpler, lower-risk without
    // tooling to verify AsyncValue plumbing). flutter_secure_storage is async-only, so a
    // cold-start session check can't be synchronous like the Angular store's localStorage
    // read; this fires the check without blocking `build()` and updates state shortly after.
    unawaited(_checkAuthStatus());
    return const AuthState();
  }

  Future<void> _checkAuthStatus() async {
    try {
      final token = await ref.read(secureStorageProvider).accessToken;
      if (token != null) {
        state = state.copyWith(isAuthenticated: true);
      }
    } catch (e) {
      // Fail open to "logged out" — a secure-storage read failure on cold start
      // (e.g. platform channel unavailable) shouldn't crash the app.
    }
  }

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

      state = state.copyWith(isLoading: false, isAuthenticated: true);
      return true;
    } on DioException catch (e) {
      final message = e.response?.statusCode == 409
          ? 'Un compte existe déjà avec cet email.'
          : "Une erreur est survenue lors de l'inscription. Veuillez réessayer.";
      // Explicitly false, not inherited: build()'s unawaited _checkAuthStatus() could
      // resolve concurrently (from a stale token already in secure storage) and set
      // isAuthenticated true right around when this failure branch runs — a failed
      // register attempt must never leave the UI showing an authenticated state.
      state = state.copyWith(isLoading: false, error: message, isAuthenticated: false);
      return false;
    } catch (e) {
      // Covers non-Dio failures too (e.g. secureStorageProvider.saveTokens throwing
      // a PlatformException) — without this, isLoading would stay stuck true.
      state = state.copyWith(
        isLoading: false,
        error: "Une erreur est survenue lors de l'inscription. Veuillez réessayer.",
        isAuthenticated: false,
      );
      return false;
    }
  }

  Future<bool> login(String email, String password) async {
    state = state.copyWith(isLoading: true, error: null);

    try {
      final dio = ref.read(apiClientProvider);
      final response = await dio.post<Map<String, dynamic>>(
        '/api/v1/auth/login',
        data: {'email': email, 'password': password},
      );

      final data = response.data!;
      await ref.read(secureStorageProvider).saveTokens(
            accessToken: data['accessToken'] as String,
            refreshToken: data['refreshToken'] as String,
          );

      state = state.copyWith(isLoading: false, isAuthenticated: true);
      return true;
    } on DioException catch (e) {
      final message = e.response?.statusCode == 401
          ? 'Email ou mot de passe incorrect.'
          : 'Une erreur est survenue lors de la connexion. Veuillez réessayer.';
      // Explicitly false — see the equivalent comment in register()'s catch block.
      state = state.copyWith(isLoading: false, error: message, isAuthenticated: false);
      return false;
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        error: 'Une erreur est survenue lors de la connexion. Veuillez réessayer.',
        isAuthenticated: false,
      );
      return false;
    }
  }

  Future<bool> forgotPassword(String email) async {
    state = state.copyWith(isLoading: true, error: null);

    try {
      await ref.read(apiClientProvider).post<void>('/api/v1/auth/forgot-password', data: {'email': email});
      state = state.copyWith(isLoading: false);
      return true;
    } catch (e) {
      // The backend always returns 200 regardless of whether the email is registered
      // (no enumeration) — this realistically only fails on a network-level error.
      state = state.copyWith(isLoading: false, error: 'Une erreur est survenue. Veuillez réessayer.');
      return false;
    }
  }

  Future<bool> resetPassword(String email, String token, String newPassword) async {
    state = state.copyWith(isLoading: true, error: null);

    try {
      final dio = ref.read(apiClientProvider);
      await dio.post<void>(
        '/api/v1/auth/reset-password',
        data: {'email': email, 'token': token, 'newPassword': newPassword},
      );

      state = state.copyWith(isLoading: false);
      return true;
    } on DioException catch (e) {
      // 400: AuthService.ResetPasswordAsync's own business-failure message (unknown email /
      // invalid or expired token). 422: FluentValidation rejected the request (e.g. an empty
      // email/token from a malformed link) — the screen-level guard should prevent this in
      // normal use, but treat it the same defensively.
      final statusCode = e.response?.statusCode;
      final message = statusCode == 400 || statusCode == 422
          ? 'Ce lien de réinitialisation est invalide ou a expiré.'
          : 'Une erreur est survenue. Veuillez réessayer.';
      state = state.copyWith(isLoading: false, error: message);
      return false;
    } catch (e) {
      state = state.copyWith(isLoading: false, error: 'Une erreur est survenue. Veuillez réessayer.');
      return false;
    }
  }

  Future<void> logout() async {
    final storage = ref.read(secureStorageProvider);
    final refreshToken = await storage.refreshToken;

    if (refreshToken != null) {
      try {
        await ref.read(apiClientProvider).post<void>('/api/v1/auth/logout', data: {'refreshToken': refreshToken});
      } catch (e) {
        // Best-effort: logging out locally must always succeed even if revoking
        // the token server-side fails (e.g. the API is temporarily unreachable).
      }
    }

    await storage.clear();
    state = state.copyWith(isAuthenticated: false);
  }
}

final authProvider = NotifierProvider<AuthNotifier, AuthState>(AuthNotifier.new);
