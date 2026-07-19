import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../shared/services/api_client.dart';

class Address {
  const Address({
    required this.id,
    required this.street,
    required this.city,
    required this.postalCode,
    required this.country,
  });

  final String id;
  final String street;
  final String city;
  final String postalCode;
  final String country;

  factory Address.fromJson(Map<String, dynamic> json) => Address(
        id: json['id'] as String,
        street: json['street'] as String,
        city: json['city'] as String,
        postalCode: json['postalCode'] as String,
        country: json['country'] as String,
      );
}

class Profile {
  const Profile({required this.name, required this.email, required this.addresses});

  final String name;
  final String email;
  final List<Address> addresses;

  factory Profile.fromJson(Map<String, dynamic> json) => Profile(
        name: json['name'] as String,
        email: json['email'] as String,
        addresses: (json['addresses'] as List<dynamic>)
            .map((a) => Address.fromJson(a as Map<String, dynamic>))
            .toList(),
      );
}

class AccountState {
  const AccountState({this.profile, this.isLoading = false, this.error});

  final Profile? profile;
  final bool isLoading;
  final String? error;

  AccountState copyWith({Profile? profile, bool? isLoading, String? error}) {
    return AccountState(
      profile: profile ?? this.profile,
      isLoading: isLoading ?? this.isLoading,
      error: error,
    );
  }
}

class AccountNotifier extends Notifier<AccountState> {
  @override
  AccountState build() => const AccountState();

  Future<void> loadProfile() async {
    state = state.copyWith(isLoading: true, error: null);

    try {
      final dio = ref.read(apiClientProvider);
      final response = await dio.get<Map<String, dynamic>>('/api/v1/account/profile');
      state = state.copyWith(isLoading: false, profile: Profile.fromJson(response.data!));
    } catch (e) {
      state = state.copyWith(isLoading: false, error: 'Impossible de charger votre profil. Veuillez réessayer.');
    }
  }

  Future<bool> updateProfile(String name, String email, String? currentPassword) async {
    state = state.copyWith(isLoading: true, error: null);

    try {
      final dio = ref.read(apiClientProvider);
      final response = await dio.patch<Map<String, dynamic>>(
        '/api/v1/account/profile',
        data: {'name': name, 'email': email, 'currentPassword': currentPassword},
      );
      state = state.copyWith(isLoading: false, profile: Profile.fromJson(response.data!));
      return true;
    } on DioException catch (e) {
      final errors = e.response?.data is Map ? e.response?.data['errors'] as List<dynamic>? : null;
      final message =
          errors != null && errors.isNotEmpty ? errors.first as String : 'Une erreur est survenue. Veuillez réessayer.';
      state = state.copyWith(isLoading: false, error: message);
      return false;
    } catch (e) {
      state = state.copyWith(isLoading: false, error: 'Une erreur est survenue. Veuillez réessayer.');
      return false;
    }
  }
}

final accountProvider = NotifierProvider<AccountNotifier, AccountState>(AccountNotifier.new);
