#nullable enable

namespace App.Net
{
    /// <summary>
    /// 服务端统一错误体：{ "code": "invalid_request", "message": "..." }。
    /// </summary>
    public sealed class ApiError
    {
        public string Code { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }

    /// <summary>客户端内置错误码（网络层用，业务错误码由服务端约定）。</summary>
    public static class ApiErrorCodes
    {
        /// <summary>请求没发出去 / 连不上 / 超时。</summary>
        public const string ConnectionError = "connection_error";

        /// <summary>响应无法解析或字段不合法。</summary>
        public const string InvalidRequest = "invalid_request";

        /// <summary>401，token 失效。</summary>
        public const string Unauthorized = "unauthorized";

        public const string Conflict = "conflict";
    }
}
