# IT-Kamianets 3D Engine: Unity Firebase

`3d-unity-firebase` connects the **IT-Kamianets 3D Engine** in Unity to Firebase.

It handles sign-in, the player's profile and saved progress, and stored scenes.

### Logic

It talks to Firebase over its **REST API** (Firebase Authentication and Cloud Firestore over HTTPS), not through the Firebase Unity SDK. The same code runs on Windows, a dedicated server, Android and iOS; nothing native to install. The SDK's desktop build is for development only, which rules it out for a PC game.

Scenes are stored as Scene JSON, as defined by `3d-scene-schema`, so anything saved from Unity can be read by other tools exactly as the schema defines it.

The package knows no project: the game passes its config (project id and API key, read from its own `google-services.json`) and deploys the security rules for the data layout below. Config and rules live in the game project, never here.

| Path | Holds | Rules (game's `firestore.rules`) |
|---|---|---|
| `users/{uid}` | the player's profile | only that player |
| `users/{uid}/progress/{key}` | saved progress, one document per key | only that player |
| `scenes/{sceneId}` | a scene: `sceneId`, `ownerUid`, `schemaVersion`, `updatedAt`, `json` (the whole Scene JSON) | signed-in players read; the owner writes |

Match results and replays (`3d-network-schema`) are written by a trusted server, not by players, so a client cannot fake them; that needs admin credentials and is on the roadmap.

`3d-unity-core`'s `EngineStorage` keeps settings on this device; this package keeps what follows the player.

Games use this package instead of writing their own Firebase integration.

### Status

Version 0.1.

* `FirebaseConfig.FromGoogleServicesJson(text)`: project id and API key from the Android config file
* `EngineFirebase.Initialize(config)` (also registered in `EngineServices`)
* `Auth`: `SignInAsync()` signs back in as this device's player or creates an anonymous account (the refresh token is kept in `EngineStorage`, so the same `Uid` comes back), tokens refresh themselves, `SignOut()`
* `Firestore`: `GetAsync`, `SetAsync`, `MergeAsync`, `DeleteAsync`, `ListAsync` with plain JSON in and out (Firestore's typed values are converted here)
* Profile: `LoadProfileAsync`, `SaveProfileAsync` (merge)
* Progress: `SaveProgressAsync<T>(key, value)`, `LoadProgressAsync<T>(key)`
* Scenes: `SaveSceneAsync(SceneData)`, `LoadSceneAsync(id)`, `ListScenesAsync()`, `DeleteSceneAsync(id)`

Checked against the real `temnorid-arena` project: anonymous sign-in and signing in again as the same player, profile save/load, progress round trip (numbers, lists, nested objects, nulls), the arena scene saved and loaded back byte-identical and listed, and the rules blocking another player's profile and a client-written match result (403).

```csharp
var config = FirebaseConfig.FromGoogleServicesJson(googleServicesJson.text);
var firebase = EngineFirebase.Initialize(config);
await firebase.Auth.SignInAsync();

await firebase.SaveProfileAsync(new JObject { ["displayName"] = "Ira" });
await firebase.SaveProgressAsync("stats", new { matches = 12, wins = 7 });
await firebase.SaveSceneAsync(SceneSaver.Capture(level));
```

### Roadmap

* Firebase setup
* Sign-in
* User profiles
* Saved progress
* Scene storage
* Scene loading
* Google sign-in on PC (browser login with a Desktop OAuth client), linking it to the anonymous account
* Match results and replays written by a trusted server
* Asset storage (Cloud Storage for asset libraries and GLB files)
* Offline queue
* Sync events
* Stable v1 Firebase API
