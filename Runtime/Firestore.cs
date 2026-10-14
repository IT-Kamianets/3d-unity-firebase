using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ITKamianets.Engine.Firebase
{
    /// <summary>One Firestore document: its id, full path, and data as plain JSON.</summary>
    public class FirestoreDocument
    {
        public string Id;
        public string Path;
        public JObject Data;
        public string UpdateTime;

        public T To<T>()
        {
            return Data.ToObject<T>();
        }
    }

    /// <summary>
    /// Cloud Firestore over REST, as the signed-in player (security rules apply). Data goes in and
    /// out as plain JSON objects; the conversion to Firestore's typed values is done here. Paths
    /// are "collection/document[/collection/document...]".
    /// </summary>
    public sealed class Firestore
    {
        private readonly FirebaseConfig _config;
        private readonly FirebaseAuth _auth;

        internal Firestore(FirebaseConfig config, FirebaseAuth auth)
        {
            _config = config;
            _auth = auth;
        }

        private string Root => $"https://firestore.googleapis.com/v1/projects/{_config.ProjectId}/databases/(default)/documents";

        /// <summary>The document, or null if it does not exist.</summary>
        public async Task<FirestoreDocument> GetAsync(string path)
        {
            try
            {
                return ToDocument(await FirebaseRequest.Send("GET", Url(path), idToken: await _auth.GetIdTokenAsync()));
            }
            catch (FirebaseException exception) when (exception.Status == 404)
            {
                return null;
            }
        }

        /// <summary>Creates the document or replaces all of its fields.</summary>
        public async Task<FirestoreDocument> SetAsync(string path, JObject data)
        {
            var body = new JObject { ["fields"] = ToFields(data) };
            return ToDocument(await FirebaseRequest.Send("PATCH", Url(path), body, await _auth.GetIdTokenAsync()));
        }

        /// <summary>Changes only the given top-level fields; others stay. Creates the document if needed.</summary>
        public async Task<FirestoreDocument> MergeAsync(string path, JObject data)
        {
            var mask = string.Join("&", data.Properties().Select(property => "updateMask.fieldPaths=" + Uri.EscapeDataString(property.Name)));
            var body = new JObject { ["fields"] = ToFields(data) };
            return ToDocument(await FirebaseRequest.Send("PATCH", Url(path) + "?" + mask, body, await _auth.GetIdTokenAsync()));
        }

        public async Task DeleteAsync(string path)
        {
            await FirebaseRequest.Send("DELETE", Url(path), idToken: await _auth.GetIdTokenAsync());
        }

        /// <summary>The documents of a collection, up to <paramref name="limit"/>.</summary>
        public async Task<List<FirestoreDocument>> ListAsync(string collectionPath, int limit = 100)
        {
            var response = await FirebaseRequest.Send("GET", $"{Url(collectionPath)}?pageSize={limit}", idToken: await _auth.GetIdTokenAsync());
            var documents = new List<FirestoreDocument>();
            if (response["documents"] is JArray array)
            {
                foreach (var item in array)
                {
                    documents.Add(ToDocument((JObject)item));
                }
            }

            return documents;
        }

        private string Url(string path)
        {
            var segments = path.Trim('/').Split('/').Select(Uri.EscapeDataString);
            return Root + "/" + string.Join("/", segments);
        }

        private static FirestoreDocument ToDocument(JObject document)
        {
            var name = (string)document["name"] ?? string.Empty;
            var marker = "/documents/";
            var index = name.IndexOf(marker, StringComparison.Ordinal);
            var path = index >= 0 ? name.Substring(index + marker.Length) : name;

            return new FirestoreDocument
            {
                Path = path,
                Id = path.Substring(path.LastIndexOf('/') + 1),
                Data = FromFields(document["fields"] as JObject),
                UpdateTime = (string)document["updateTime"],
            };
        }

        // ---- Plain JSON <-> Firestore typed values ----

        internal static JObject ToFields(JObject data)
        {
            var fields = new JObject();
            foreach (var property in data.Properties())
            {
                fields[property.Name] = ToValue(property.Value);
            }

            return fields;
        }

        internal static JObject FromFields(JObject fields)
        {
            var data = new JObject();
            if (fields == null)
            {
                return data;
            }

            foreach (var property in fields.Properties())
            {
                data[property.Name] = FromValue((JObject)property.Value);
            }

            return data;
        }

        private static JObject ToValue(JToken token)
        {
            switch (token?.Type ?? JTokenType.Null)
            {
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return new JObject { ["nullValue"] = null };
                case JTokenType.Boolean:
                    return new JObject { ["booleanValue"] = token };
                case JTokenType.Integer:
                    return new JObject { ["integerValue"] = token.ToString() };
                case JTokenType.Float:
                    return new JObject { ["doubleValue"] = token };
                case JTokenType.Array:
                    return new JObject { ["arrayValue"] = new JObject { ["values"] = new JArray(token.Select(ToValue)) } };
                case JTokenType.Object:
                    return new JObject { ["mapValue"] = new JObject { ["fields"] = ToFields((JObject)token) } };
                default:
                    return new JObject { ["stringValue"] = token.ToString() };
            }
        }

        private static JToken FromValue(JObject value)
        {
            var property = value.Properties().FirstOrDefault();
            if (property == null)
            {
                return JValue.CreateNull();
            }

            var inner = property.Value;
            switch (property.Name)
            {
                case "nullValue":
                    return JValue.CreateNull();
                case "integerValue":
                    return long.Parse((string)inner, CultureInfo.InvariantCulture);
                case "doubleValue":
                    return (double)inner;
                case "arrayValue":
                    return new JArray(((inner["values"] as JArray) ?? new JArray()).Select(item => FromValue((JObject)item)));
                case "mapValue":
                    return FromFields(inner["fields"] as JObject);
                default:
                    // booleanValue, stringValue, timestampValue, referenceValue, bytesValue, geoPointValue
                    return inner.DeepClone();
            }
        }
    }
}
