import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'secure_storage.dart';

// Overridable via `flutter build --dart-define=API_BASE_URL=...`; defaults to the
// backend's local dev port (see Story 1.9 research: launchSettings.json -> 5287).
const String _baseUrl = String.fromEnvironment('API_BASE_URL', defaultValue: 'http://localhost:5287');

// Requests to these paths must never trigger the refresh-and-retry logic below — a 401
// from /auth/refresh itself means the session truly can't be recovered. /auth/logout is
// excluded too: AuthNotifier.logout() already treats any failure there as best-effort and
// clears local state regardless, so there's no need to refresh-and-retry a logout call —
// doing so would just flip isAuthenticated true again right before logout flips it back false.
const _authPaths = [
  '/api/v1/auth/login',
  '/api/v1/auth/register',
  '/api/v1/auth/refresh',
  '/api/v1/auth/logout',
  '/api/v1/auth/forgot-password',
  '/api/v1/auth/reset-password',
];

// Marks a request as already having gone through one refresh-and-retry cycle, so a second
// 401 on the retried request (e.g. the backend 401s that resource for an unrelated reason)
// propagates instead of looping refresh-and-retry forever.
const _retriedFlag = 'authInterceptorRetried';

final apiClientProvider = Provider<Dio>((ref) {
  final dio = Dio(BaseOptions(baseUrl: _baseUrl));
  final secureStorage = ref.read(secureStorageProvider);

  // Dedupes concurrent refresh attempts: if several requests 401 at once, only the
  // first should call /auth/refresh — the backend rotates (revokes the old token), so
  // a second concurrent call using the now-revoked token would fail even though the
  // session is fine. Same reasoning as the Angular interceptor's refresh dedup.
  Future<bool>? refreshInFlight;

  Future<bool> refresh() {
    return refreshInFlight ??= () async {
      try {
        final refreshToken = await secureStorage.refreshToken;
        if (refreshToken == null) return false;

        final response = await Dio(BaseOptions(baseUrl: _baseUrl)).post<Map<String, dynamic>>(
          '/api/v1/auth/refresh',
          data: {'refreshToken': refreshToken},
        );

        final data = response.data!;
        await secureStorage.saveTokens(
          accessToken: data['accessToken'] as String,
          refreshToken: data['refreshToken'] as String,
        );
        return true;
      } catch (e) {
        await secureStorage.clear();
        return false;
      } finally {
        refreshInFlight = null;
      }
    }();
  }

  dio.interceptors.add(
    InterceptorsWrapper(
      onRequest: (options, handler) async {
        final token = await secureStorage.accessToken;
        if (token != null) {
          options.headers['Authorization'] = 'Bearer $token';
        }
        handler.next(options);
      },
      onError: (error, handler) async {
        final isAuthPath = _authPaths.any((path) => error.requestOptions.path.contains(path));
        final alreadyRetried = error.requestOptions.extra[_retriedFlag] == true;

        if (error.response?.statusCode != 401 || isAuthPath || alreadyRetried) {
          handler.next(error);
          return;
        }

        final refreshed = await refresh();
        if (!refreshed) {
          handler.next(error);
          return;
        }

        try {
          final token = await secureStorage.accessToken;
          final retriedOptions = error.requestOptions;
          retriedOptions.extra[_retriedFlag] = true;
          if (token != null) {
            retriedOptions.headers['Authorization'] = 'Bearer $token';
          }
          final response = await dio.fetch<dynamic>(retriedOptions);
          handler.resolve(response);
        } on DioException catch (retryError) {
          handler.next(retryError);
        }
      },
    ),
  );

  return dio;
});
