using System;
using System.Threading.Tasks;
using ITKamianets.Engine.Core;
using Newtonsoft.Json.Linq;

namespace ITKamianets.Engine.Firebase
{
    /// <summary>
    /// Firebase Authentication over REST. v0.1 signs players in anonymously: an account is made
    /// on first start and kept on this device (its refresh token is saved in <see cref="EngineStorage"/>),
    /// so the same player comes back with the same <see cref="Uid"/> and data. Tokens refresh themselves.
    /// </summary>
    public sealed class FirebaseAuth
    {
        private const string LogCategory = "Firebase";
        private const string RefreshTokenKey = "firebase.refreshToken";

        private readonly FirebaseConfig _config;
        private string _idToken;
        private string _refreshToken;
        private DateTime _expiresAt;

        internal FirebaseAuth(FirebaseConfig config)
        {
            _config = config;
        }

        /// <summary>The signed-in player's id, or null.</summary>
        public string Uid { get; private set; }

        public bool IsSignedIn => Uid != null;

        public event Action<string> SignedIn;
        public event Action SignedOut;

        /// <summary>Signs back in as this device's player, or creates an anonymous account the first time.</summary>
        public async Task<string> SignInAsync()
        {
            var saved = EngineStorage.Current.GetString(RefreshTokenKey);
            if (!string.IsNullOrEmpty(saved))
            {
                try
                {
                    await Refresh(saved);
                    EngineLog.Info(LogCategory, $"Signed in again as {Uid}.");
                    SignedIn?.Invoke(Uid);
                    return Uid;
                }
                catch (FirebaseException exception)
                {
                    EngineLog.Warning(LogCategory, $"Saved sign-in no longer works ({exception.Message}); creating a new account.");
                }
            }

            return await SignInAnonymouslyAsync();
        }

        /// <summary>Always creates a new anonymous account.</summary>
        public async Task<string> SignInAnonymouslyAsync()
        {
            var response = await FirebaseRequest.Send("POST", $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={_config.ApiKey}",
                new JObject { ["returnSecureToken"] = true });

            Store((string)response["idToken"], (string)response["refreshToken"], (string)response["localId"], (string)response["expiresIn"]);
            EngineLog.Info(LogCategory, $"New anonymous account {Uid}.");
            SignedIn?.Invoke(Uid);
            return Uid;
        }

        /// <summary>Forgets this device's account. Its data stays in Firebase but this device can no longer reach it.</summary>
        public void SignOut()
        {
            Uid = null;
            _idToken = null;
            _refreshToken = null;
            EngineStorage.Current.Delete(RefreshTokenKey);
            EngineStorage.Current.Save();
            SignedOut?.Invoke();
        }

        /// <summary>A valid ID token for authenticated calls, refreshed when it is about to expire.</summary>
        public async Task<string> GetIdTokenAsync()
        {
            if (!IsSignedIn)
            {
                throw new InvalidOperationException("Not signed in to Firebase.");
            }

            if (DateTime.UtcNow > _expiresAt.AddMinutes(-2))
            {
                await Refresh(_refreshToken);
            }

            return _idToken;
        }

        private async Task Refresh(string refreshToken)
        {
            var response = await FirebaseRequest.Send("POST", $"https://securetoken.googleapis.com/v1/token?key={_config.ApiKey}",
                formBody: "grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(refreshToken));

            Store((string)response["id_token"], (string)response["refresh_token"], (string)response["user_id"], (string)response["expires_in"]);
        }

        private void Store(string idToken, string refreshToken, string uid, string expiresIn)
        {
            _idToken = idToken;
            _refreshToken = refreshToken;
            Uid = uid;
            _expiresAt = DateTime.UtcNow.AddSeconds(int.TryParse(expiresIn, out var seconds) ? seconds : 3600);

            EngineStorage.Current.SetString(RefreshTokenKey, refreshToken);
            EngineStorage.Current.Save();
        }
    }
}
