using System;
using System.Text;
using System.Threading.Tasks;
using Framework.Log;
using Framework.Save;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using UnityEngine.Networking;

namespace App.Net
{
    /// <summary>
    /// 通用 HTTP + JSON 客户端：GET / POST / PUT / DELETE，自动带 Bearer token，
    /// 401 自动清 token，非 2xx 抛 <see cref="GameApiException"/>。
    ///
    /// 业务层按需继承或在外部封装具体的 URL 与 DTO，本类不含任何业务端点。
    /// </summary>
    public sealed class HttpApiClient
    {
        /// <summary>token 在存档里的 key。</summary>
        public const string TokenSaveKey = "net.auth.token.v1";

        private const int TimeoutSeconds = 15;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        private readonly ISaveService _save;

        public HttpApiClient(ISaveService save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
        }

        /// <summary>当前登录 token（空字符串表示未登录）。</summary>
        public string Token
        {
            get => _save.GetString(TokenSaveKey, string.Empty);
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _save.DeleteKey(TokenSaveKey);
                }
                else
                {
                    _save.SetString(TokenSaveKey, value);
                }

                _save.Save();
            }
        }

        public bool HasToken => !string.IsNullOrEmpty(Token);

        public void ClearToken() => Token = string.Empty;

        public Task<T> GetAsync<T>(string path) =>
            SendAsync<T>(UnityWebRequest.kHttpVerbGET, path, null);

        public Task<T> PostAsync<T>(string path, object body = null) =>
            SendAsync<T>(UnityWebRequest.kHttpVerbPOST, path, body);

        public Task<T> PutAsync<T>(string path, object body = null) =>
            SendAsync<T>(UnityWebRequest.kHttpVerbPUT, path, body);

        public Task<T> DeleteAsync<T>(string path) =>
            SendAsync<T>(UnityWebRequest.kHttpVerbDELETE, path, null);

        /// <summary>发请求并反序列化为 <typeparamref name="T"/>；返回空 body 时返回 default。</summary>
        public async Task<T> SendAsync<T>(string method, string path, object body)
        {
            var text = await SendRawAsync(method, path, body);
            if (string.IsNullOrWhiteSpace(text))
            {
                return default;
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(text, JsonSettings);
            }
            catch (JsonException e)
            {
                throw new GameApiException(
                    ApiErrorCodes.InvalidRequest,
                    $"Failed to parse response of {method} {path}: {e.Message}");
            }
        }

        /// <summary>发请求并返回原始响应文本（空 body 返回空字符串）。</summary>
        public async Task<string> SendRawAsync(string method, string path, object body)
        {
            var url = GameApiSettings.BuildUrl(path);
            var payload = body == null ? null : JsonConvert.SerializeObject(body, JsonSettings);

            using var request = new UnityWebRequest(url, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds,
            };

            if (payload != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                request.SetRequestHeader("Content-Type", "application/json");
            }

            var token = Token;
            if (!string.IsNullOrEmpty(token))
            {
                request.SetRequestHeader("Authorization", "Bearer " + token);
            }

            AppLog.Debug(LogChannel.Net, $"{method} {url}");

            // UnityWebRequestAsyncOperation 在 Unity 里没有 GetAwaiter，用轮询 Task.Yield 兼容各平台。
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            var status = (int)request.responseCode;
            var responseText = request.downloadHandler?.text ?? string.Empty;

            if (request.result == UnityWebRequest.Result.Success)
            {
                return responseText;
            }

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.DataProcessingError)
            {
                throw new GameApiException(
                    ApiErrorCodes.ConnectionError,
                    string.IsNullOrEmpty(request.error) ? "Network error." : request.error,
                    status);
            }

            if (status == 401)
            {
                ClearToken();
            }

            throw ParseError(responseText, status);
        }

        private static GameApiException ParseError(string responseText, int status)
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return new GameApiException(
                    ApiErrorCodes.InvalidRequest,
                    $"HTTP {status}",
                    status);
            }

            try
            {
                var error = JsonConvert.DeserializeObject<ApiError>(responseText, JsonSettings);
                if (error != null && !string.IsNullOrEmpty(error.Code))
                {
                    return new GameApiException(error.Code, error.Message, status);
                }
            }
            catch (JsonException)
            {
                // 非 JSON 错误体，落到下面按原文返回。
            }

            return new GameApiException(
                ApiErrorCodes.InvalidRequest,
                string.IsNullOrEmpty(responseText) ? $"HTTP {status}" : responseText,
                status);
        }
    }
}
