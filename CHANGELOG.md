# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial Unity package structure (`Runtime`, `Editor`, `Tests` assemblies).
- Firebase over REST (no SDK): `FirebaseConfig` (from google-services.json), `FirebaseAuth` (anonymous, kept per device, token refresh), `Firestore` (get/set/merge/delete/list with plain JSON), `EngineFirebase` (profile, progress, scene storage).
- Email and password sign-in: `SignUpWithEmailAsync`, `SignInWithEmailAsync`, `SendPasswordResetAsync`, and `TrySignInAgainAsync` to restore the saved account without creating an anonymous one.
- Google sign-in on PC: `SignInWithGoogleAsync(clientId, clientSecret)` opens the browser and catches the answer on a local loopback address (OAuth for desktop apps with PKCE), then trades the Google ID token for a Firebase account.
