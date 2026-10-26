using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ITKamianets.Engine.Firebase
{
    /// <summary>
    /// Google sign-in for PC: opens the system browser on Google's consent page and catches the answer on a
    /// local loopback address (OAuth 2.0 for desktop apps, with PKCE). Gives back a Google ID token that
    /// <see cref="FirebaseAuth"/> trades for a Firebase account.
    /// </summary>
    internal static class GoogleDesktopSignIn
    {
        private const string AuthUrl = "https://accounts.google.com/o/oauth2/v2/auth";
        private const string TokenUrl = "https://oauth2.googleapis.com/token";
        private const int TimeoutSeconds = 180;

        private const string DonePage =
            "<!doctype html><meta charset=\"utf-8\"><title>VRoom</title>" +
            "<body style=\"font-family:sans-serif;text-align:center;margin-top:20vh\">" +
            "<h2>Готово, можна повернутися до гри.</h2><p>You can close this tab and go back to the game.</p></body>";

        /// <summary>Runs the browser login and returns the Google ID token. Throws when the player cancels or it times out.</summary>
        public static async Task<string> GetIdTokenAsync(string clientId, string clientSecret)
        {
            var verifier = RandomString(64);
            var state = RandomString(16);
            var port = FreePort();
            var redirectUri = $"http://127.0.0.1:{port}/";

            var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri);
            listener.Start();
            try
            {
                var url = AuthUrl +
                          "?client_id=" + Uri.EscapeDataString(clientId) +
                          "&redirect_uri=" + Uri.EscapeDataString(redirectUri) +
                          "&response_type=code&scope=" + Uri.EscapeDataString("openid email profile") +
                          "&code_challenge=" + Challenge(verifier) + "&code_challenge_method=S256" +
                          "&state=" + state;
                Application.OpenURL(url);

                var contextTask = listener.GetContextAsync();
                if (await Task.WhenAny(contextTask, Task.Delay(TimeoutSeconds * 1000)) != contextTask)
                {
                    throw new TimeoutException("Google sign-in timed out.");
                }

                var context = await contextTask;
                var query = context.Request.QueryString;
                var page = Encoding.UTF8.GetBytes(DonePage);
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.OutputStream.Write(page, 0, page.Length);
                context.Response.Close();

                if (query["error"] != null)
                {
                    throw new FirebaseException(0, "Google sign-in: " + query["error"]);
                }

                if (query["state"] != state || string.IsNullOrEmpty(query["code"]))
                {
                    throw new FirebaseException(0, "Google sign-in: unexpected answer.");
                }

                var form = "code=" + Uri.EscapeDataString(query["code"]) +
                           "&client_id=" + Uri.EscapeDataString(clientId) +
                           "&client_secret=" + Uri.EscapeDataString(clientSecret) +
                           "&code_verifier=" + verifier +
                           "&redirect_uri=" + Uri.EscapeDataString(redirectUri) +
                           "&grant_type=authorization_code";
                JObject response = await FirebaseRequest.Send("POST", TokenUrl, formBody: form);
                return (string)response["id_token"];
            }
            finally
            {
                listener.Close();
            }
        }

        private static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        private static string RandomString(int length)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
            var bytes = new byte[length];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            var chars = new char[length];
            for (var i = 0; i < length; i++)
            {
                chars[i] = alphabet[bytes[i] % alphabet.Length];
            }

            return new string(chars);
        }

        private static string Challenge(string verifier)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.ASCII.GetBytes(verifier));
                return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            }
        }
    }
}
