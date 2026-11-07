using System;

namespace App.Net
{
    /// <summary>AppBootstrap 上可选的服务器地址预设。</summary>
    public enum GameApiServerEndpoint
    {
        /// <summary>本机（127.0.0.1，配合本地开发服）。</summary>
        LocalHost = 0,

        /// <summary>自定义 IP/域名与端口。</summary>
        Custom = 1,

        /// <summary>远程服（<see cref="GameApiSettings.RemoteBaseUrl"/>，部署后改成自己的域名）。</summary>
        RemoteDefault = 2,
    }

    /// <summary>
    /// 局外 HTTP 短链地址。由 AppBootstrap 的序列化字段在启动时写入。
    /// 不使用联网功能时 <see cref="Enabled"/> 保持 false，客户端不会发任何请求。
    /// </summary>
    public static class GameApiSettings
    {
        public const int DefaultPort = 5254;

        /// <summary>本机开发服。</summary>
        public const string LocalHostBaseUrl = "http://127.0.0.1:5254";

        /// <summary>远程服地址，部署时替换成自己的域名（含端口，不含尾斜杠）。</summary>
        public const string RemoteBaseUrl = "https://api.example.com";

        /// <summary>当前生效的 HTTP BaseUrl（不含尾斜杠）。</summary>
        public static string BaseUrl { get; set; } = LocalHostBaseUrl;

        /// <summary>是否连接服务器。关闭后启动不拉取任何远端数据，走纯本地流程。</summary>
        public static bool Enabled { get; set; }

        /// <summary>按 Bootstrap 上的预设解析 HTTP BaseUrl（不含尾斜杠）。</summary>
        public static string ResolveBaseUrl(
            GameApiServerEndpoint preset,
            string customHost,
            int customPort)
        {
            switch (preset)
            {
                case GameApiServerEndpoint.Custom:
                {
                    var host = string.IsNullOrWhiteSpace(customHost) ? "127.0.0.1" : customHost.Trim();
                    var port = customPort <= 0 ? DefaultPort : customPort;
                    return $"http://{host}:{port}";
                }
                case GameApiServerEndpoint.RemoteDefault:
                    return RemoteBaseUrl.TrimEnd('/');
                case GameApiServerEndpoint.LocalHost:
                default:
                    return LocalHostBaseUrl;
            }
        }

        /// <summary>拼出完整请求地址（path 可带或不带前导斜杠）。</summary>
        public static string BuildUrl(string path)
        {
            var baseUrl = (BaseUrl ?? LocalHostBaseUrl).TrimEnd('/');
            if (string.IsNullOrEmpty(path))
            {
                return baseUrl;
            }

            return path[0] == '/' ? baseUrl + path : baseUrl + "/" + path;
        }
    }
}
