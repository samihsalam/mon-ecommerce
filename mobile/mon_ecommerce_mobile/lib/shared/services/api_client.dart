import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'secure_storage.dart';

// Overridable via `flutter build --dart-define=API_BASE_URL=...`; defaults to the
// backend's local dev port (see Story 1.9 research: launchSettings.json -> 5287).
const String _baseUrl = String.fromEnvironment('API_BASE_URL', defaultValue: 'http://localhost:5287');

final apiClientProvider = Provider<Dio>((ref) {
  final dio = Dio(BaseOptions(baseUrl: _baseUrl));
  final secureStorage = ref.read(secureStorageProvider);

  // Attaches the stored access token to outgoing requests. 401-refresh-and-retry
  // is explicitly Story 2.2's scope, not this one's.
  dio.interceptors.add(
    InterceptorsWrapper(
      onRequest: (options, handler) async {
        final token = await secureStorage.accessToken;
        if (token != null) {
          options.headers['Authorization'] = 'Bearer $token';
        }
        handler.next(options);
      },
    ),
  );

  return dio;
});
