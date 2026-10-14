using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ITKamianets.Engine.Core;
using ITKamianets.Engine.Scene;
using ITKamianets.Engine.Scene.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ITKamianets.Engine.Firebase
{
    /// <summary>
    /// Firebase for a game: sign-in, the player's profile and saved progress, and stored scenes,
    /// over the REST API. The data layout (and the security rules a game deploys for it):
    /// <list type="bullet">
    /// <item><c>users/{uid}</c>: the player's profile (only that player);</item>
    /// <item><c>users/{uid}/progress/{key}</c>: saved progress (only that player);</item>
    /// <item><c>scenes/{sceneId}</c>: Scene JSON with its <c>ownerUid</c> (signed-in players read, the owner writes).</item>
    /// </list>
    /// <code>
    /// var firebase = EngineFirebase.Initialize(FirebaseConfig.FromGoogleServicesJson(configText));
    /// await firebase.Auth.SignInAsync();
    /// await firebase.SaveProgressAsync("settings", mySettings);
    /// </code>
    /// </summary>
    public sealed class EngineFirebase
    {
        public const string UsersCollection = "users";
        public const string ProgressCollection = "progress";
        public const string ScenesCollection = "scenes";

        /// <summary>The instance made by <see cref="Initialize"/>, also registered in <see cref="EngineServices"/>.</summary>
        public static EngineFirebase Current => EngineServices.TryGet<EngineFirebase>(out var firebase) ? firebase : null;

        public FirebaseConfig Config { get; }
        public FirebaseAuth Auth { get; }
        public Firestore Firestore { get; }

        private EngineFirebase(FirebaseConfig config)
        {
            Config = config;
            Auth = new FirebaseAuth(config);
            Firestore = new Firestore(config, Auth);
        }

        public static EngineFirebase Initialize(FirebaseConfig config)
        {
            var firebase = new EngineFirebase(config);
            EngineServices.Set(firebase);
            return firebase;
        }

        // ---- Profile ----

        /// <summary>The signed-in player's profile document as JSON, or null if none was saved yet.</summary>
        public async Task<JObject> LoadProfileAsync()
        {
            return (await Firestore.GetAsync(UserPath()))?.Data;
        }

        /// <summary>Merges fields into the profile (displayName, game fields ...), stamping updatedAt.</summary>
        public Task SaveProfileAsync(JObject fields)
        {
            fields = (JObject)fields.DeepClone();
            fields["updatedAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            return Firestore.MergeAsync(UserPath(), fields);
        }

        // ---- Progress ----

        /// <summary>Saves an object (as JSON, Newtonsoft rules) under a key, e.g. "unlocks", "stats".</summary>
        public Task SaveProgressAsync<T>(string key, T value)
        {
            var data = new JObject
            {
                ["value"] = JToken.FromObject(value),
                ["updatedAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            };
            return Firestore.SetAsync($"{UserPath()}/{ProgressCollection}/{key}", data);
        }

        /// <summary>The object saved under a key, or <paramref name="fallback"/> if none.</summary>
        public async Task<T> LoadProgressAsync<T>(string key, T fallback = default)
        {
            var document = await Firestore.GetAsync($"{UserPath()}/{ProgressCollection}/{key}");
            var value = document?.Data["value"];
            return value == null || value.Type == JTokenType.Null ? fallback : value.ToObject<T>();
        }

        // ---- Scenes ----

        /// <summary>
        /// Stores a scene under its sceneId, owned by the signed-in player. The whole Scene JSON is
        /// kept as one string field, so any tool can read it back exactly as 3d-scene-schema defines it.
        /// </summary>
        public Task SaveSceneAsync(SceneData scene)
        {
            var data = new JObject
            {
                ["sceneId"] = scene.SceneId,
                ["ownerUid"] = Auth.Uid,
                ["schemaVersion"] = scene.SchemaVersion,
                ["updatedAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                ["json"] = SceneJson.Serialize(scene),
            };
            return Firestore.SetAsync($"{ScenesCollection}/{scene.SceneId}", data);
        }

        /// <summary>A stored scene, or null.</summary>
        public async Task<SceneData> LoadSceneAsync(string sceneId)
        {
            var document = await Firestore.GetAsync($"{ScenesCollection}/{sceneId}");
            var json = (string)document?.Data["json"];
            return json == null ? null : SceneJson.Deserialize(json);
        }

        /// <summary>Ids and owners of stored scenes, without their content.</summary>
        public async Task<List<(string SceneId, string OwnerUid, string UpdatedAt)>> ListScenesAsync(int limit = 100)
        {
            var list = new List<(string, string, string)>();
            foreach (var document in await Firestore.ListAsync(ScenesCollection, limit))
            {
                list.Add(((string)document.Data["sceneId"] ?? document.Id, (string)document.Data["ownerUid"], (string)document.Data["updatedAt"]));
            }

            return list;
        }

        public Task DeleteSceneAsync(string sceneId)
        {
            return Firestore.DeleteAsync($"{ScenesCollection}/{sceneId}");
        }

        private string UserPath()
        {
            if (!Auth.IsSignedIn)
            {
                throw new InvalidOperationException("Sign in first: await EngineFirebase.Auth.SignInAsync().");
            }

            return $"{UsersCollection}/{Auth.Uid}";
        }
    }
}
