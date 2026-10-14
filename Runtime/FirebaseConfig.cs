using System;
using Newtonsoft.Json.Linq;

namespace ITKamianets.Engine.Firebase
{
    /// <summary>
    /// Which Firebase project to talk to. Not secret (every client has it); access is controlled
    /// by the project's security rules. Keep it in the game project, never in an engine package.
    /// </summary>
    [Serializable]
    public class FirebaseConfig
    {
        public string ProjectId;
        public string ApiKey;

        /// <summary>Reads the project id and API key from a google-services.json (the Android config file).</summary>
        public static FirebaseConfig FromGoogleServicesJson(string json)
        {
            var root = JObject.Parse(json);
            var config = new FirebaseConfig
            {
                ProjectId = (string)root.SelectToken("project_info.project_id"),
                ApiKey = (string)root.SelectToken("client[0].api_key[0].current_key"),
            };

            if (string.IsNullOrEmpty(config.ProjectId) || string.IsNullOrEmpty(config.ApiKey))
            {
                throw new FormatException("google-services.json has no project_info.project_id or client api_key.");
            }

            return config;
        }
    }
}
