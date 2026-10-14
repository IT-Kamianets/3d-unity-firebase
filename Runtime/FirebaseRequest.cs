using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace ITKamianets.Engine.Firebase
{
    /// <summary>A failed Firebase call, with the HTTP status and Firebase's own error message.</summary>
    public class FirebaseException : Exception
    {
        public long Status { get; }

        public FirebaseException(long status, string message) : base(message)
        {
            Status = status;
        }
    }

    /// <summary>HTTPS calls to Firebase's REST endpoints with UnityWebRequest, awaitable.</summary>
    internal static class FirebaseRequest
    {
        public static Task<JObject> Send(string method, string url, JObject body = null, string idToken = null, string formBody = null)
        {
            var request = new UnityWebRequest(url, method) { downloadHandler = new DownloadHandlerBuffer() };
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Newtonsoft.Json.Formatting.None)));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            else if (formBody != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(formBody));
                request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            }

            if (idToken != null)
            {
                request.SetRequestHeader("Authorization", "Bearer " + idToken);
            }

            var completion = new TaskCompletionSource<JObject>();
            request.SendWebRequest().completed += _ =>
            {
                try
                {
                    var text = request.downloadHandler.text;
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        completion.SetResult(string.IsNullOrEmpty(text) ? new JObject() : JObject.Parse(text));
                    }
                    else
                    {
                        completion.SetException(new FirebaseException(request.responseCode, ErrorMessage(text) ?? request.error));
                    }
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
                finally
                {
                    request.Dispose();
                }
            };

            return completion.Task;
        }

        private static string ErrorMessage(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            try
            {
                var error = JObject.Parse(text)["error"];
                return error?.Type == JTokenType.Object ? (string)error["message"] : (string)error;
            }
            catch
            {
                return text;
            }
        }
    }
}
