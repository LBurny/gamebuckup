using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GameBackup.Core.Models;

namespace GameBackup.Core.Llm
{
    public sealed class LlmClient
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

        static LlmClient()
        {
            try
            {
                // Playnite hosts the plugin on .NET Framework, where the process-wide
                // protocol list can still be TLS 1.0/SSL3. Adding TLS 1.2 is additive
                // and keeps the handshake working on every supported host.
                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
            }
            catch
            {
                // Older runtimes without the enum value keep their defaults.
            }
        }

        public async Task<LlmTestResult> TestAsync(LlmOptions options, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var reply = await ChatWithTransportRetryAsync(
                    options,
                    "You are a connectivity probe. Follow the instruction exactly.",
                    "Reply with exactly: ok",
                    cancellationToken).ConfigureAwait(false);

                stopwatch.Stop();
                return new LlmTestResult
                {
                    Success = true,
                    Model = options.Model,
                    Message = (reply ?? string.Empty).Trim(),
                    Latency = stopwatch.Elapsed,
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new LlmTestResult
                {
                    Success = false,
                    Model = options.Model,
                    Message = ex is OperationCanceledException ? "请求超时或被取消。" : Describe(ex, options),
                    Latency = stopwatch.Elapsed,
                };
            }
        }

        /// <summary>
        /// Transport failures surface as a generic "error while sending" message on
        /// .NET Framework; the real cause only lives in the inner exceptions, so
        /// the test result keeps the whole chain plus the TLS/proxy context.
        /// </summary>
        private static string Describe(Exception ex, LlmOptions options)
        {
            var builder = new StringBuilder(ex.Message);
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
            {
                builder.Append(" ← ").Append(inner.Message);
            }

            if (ex is HttpRequestException)
            {
                builder.Append("（TLS：").Append(System.Net.ServicePointManager.SecurityProtocol).Append('）');

                try
                {
                    var proxy = System.Net.WebRequest.DefaultWebProxy?.GetProxy(new Uri(options.Endpoint));
                    builder.Append(proxy == null ? "（未使用代理）" : "（代理：" + proxy.Authority + "）");
                }
                catch
                {
                    // Diagnostics are best-effort.
                }
            }

            return builder.ToString();
        }

        public async Task<IReadOnlyList<SaveCandidate>> LocateAsync(
            GameDescriptor game,
            string homeDirectory,
            LlmOptions options,
            CancellationToken cancellationToken)
        {
            var home = (homeDirectory ?? string.Empty).TrimEnd('\\', '/');
            var context = LlmContextBuilder.Build(game, home);
            var content = await ChatWithTransportRetryAsync(
                options,
                context.SystemPrompt,
                context.UserPrompt,
                cancellationToken).ConfigureAwait(false);

            var json = ExtractJson(content);
            var response = Deserialize<LocateResponse>(json);
            var results = new List<SaveCandidate>();

            if (response?.Candidates == null)
            {
                return results;
            }

            foreach (var item in response.Candidates)
            {
                if (results.Count >= 5)
                {
                    break;
                }

                var path = NormalizeCandidatePath(item.Path, home);
                if (path == null || !IsAllowed(path, home, game.InstallDirectory))
                {
                    continue;
                }

                var isFile = File.Exists(path);
                var isDirectory = Directory.Exists(path);
                if (!isFile && !isDirectory)
                {
                    continue;
                }

                if (results.Any(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                results.Add(new SaveCandidate
                {
                    Path = path,
                    Kind = isFile ? CandidateKind.File : CandidateKind.Directory,
                    Confidence = Math.Max(0.0, Math.Min(0.65, item.Confidence)),
                    Source = CandidateSource.Llm,
                    Evidence = string.IsNullOrWhiteSpace(item.Reason) ? "AI 根据目录结构推断" : item.Reason.Trim(),
                    Exists = true,
                    SizeBytes = isFile ? SafeFileSize(path) : 0,
                    LastWriteUtc = SafeLastWrite(path),
                });
            }

            return results
                .OrderByDescending(x => x.Confidence)
                .ToList();
        }

        /// <summary>
        /// Transport hiccups (a proxy that just restarted, an idle socket) show up
        /// as <see cref="HttpRequestException"/> before anything reaches the
        /// server, so one quick retry is safe: a finished request never repeats.
        /// </summary>
        private static async Task<string> ChatWithTransportRetryAsync(
            LlmOptions options,
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await ChatAsync(options, systemPrompt, userPrompt, cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException) when (attempt < 2 && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(400, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static async Task<string> ChatAsync(
            LlmOptions options,
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken)
        {
            if (options == null
                || string.IsNullOrWhiteSpace(options.Endpoint)
                || string.IsNullOrWhiteSpace(options.Model)
                || string.IsNullOrWhiteSpace(options.ApiKey))
            {
                throw new InvalidOperationException("AI 服务地址、模型或 API Key 未填写完整。");
            }

            var url = options.Endpoint.TrimEnd('/');
            if (!url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                url += "/chat/completions";
            }

            var request = new ChatRequest
            {
                Model = options.Model,
                Temperature = 0,
                Stream = false,
                Messages = new[]
                {
                    new ChatMessage { Role = "system", Content = systemPrompt },
                    new ChatMessage { Role = "user", Content = userPrompt },
                },
            };

            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds)));

                using (var message = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
                    message.Headers.UserAgent.ParseAdd("GameBackup/0.1");
                    message.Content = new StringContent(Serialize(request), Encoding.UTF8, "application/json");

                    using (var response = await Http.SendAsync(message, timeout.Token).ConfigureAwait(false))
                    {
                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode)
                        {
                            var detail = TryReadError(body) ?? Truncate(body, 400);
                            throw new InvalidOperationException(
                                $"AI 请求失败（HTTP {(int)response.StatusCode}）：{detail}");
                        }

                        var parsed = Deserialize<ChatResponse>(body);
                        if (parsed?.Error != null && !string.IsNullOrWhiteSpace(parsed.Error.Message))
                        {
                            throw new InvalidOperationException("AI 返回错误：" + parsed.Error.Message);
                        }

                        var content = parsed?.Choices != null && parsed.Choices.Length > 0
                            ? parsed.Choices[0].Message?.Content
                            : null;

                        if (string.IsNullOrWhiteSpace(content))
                        {
                            throw new InvalidOperationException("AI 响应中没有可用的 message.content。");
                        }

                        return content;
                    }
                }
            }
        }

        private static string ExtractJson(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException("AI 返回了空内容。");
            }

            var text = content.Trim();

            if (text.StartsWith("```", StringComparison.Ordinal))
            {
                var firstLineEnd = text.IndexOf('\n');
                if (firstLineEnd >= 0)
                {
                    text = text.Substring(firstLineEnd + 1);
                }
                var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
                if (lastFence >= 0)
                {
                    text = text.Substring(0, lastFence);
                }
            }

            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                throw new InvalidOperationException("AI 返回的内容不包含 JSON：" + Truncate(text, 200));
            }

            return text.Substring(start, end - start + 1);
        }

        private static string NormalizeCandidatePath(string raw, string home)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var value = raw.Trim().Trim('"');
            if (!string.IsNullOrEmpty(home))
            {
                value = TextUtil.ReplaceIgnoreCase(value, "<home>", home);
            }

            value = value.Replace('/', '\\');

            try
            {
                if (!Path.IsPathRooted(value))
                {
                    return null;
                }
                return Path.GetFullPath(value).TrimEnd('\\');
            }
            catch
            {
                return null;
            }
        }

        private static bool IsAllowed(string path, string home, string installDirectory)
        {
            if (IsUnder(path, home) || IsUnder(path, installDirectory))
            {
                return true;
            }

            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (IsUnder(path, programData))
            {
                return true;
            }

            var publicDocuments = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
            return IsUnder(path, publicDocuments);
        }

        private static bool IsUnder(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
            {
                return false;
            }

            string fullPath;
            string fullRoot;
            try
            {
                fullPath = Path.GetFullPath(path).TrimEnd('\\');
                fullRoot = Path.GetFullPath(root).TrimEnd('\\');
            }
            catch
            {
                return false;
            }

            return fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
                   || fullPath.StartsWith(fullRoot + "\\", StringComparison.OrdinalIgnoreCase);
        }

        private static long SafeFileSize(string path)
        {
            try
            {
                return new FileInfo(path).Length;
            }
            catch
            {
                return 0;
            }
        }

        private static DateTime? SafeLastWrite(string path)
        {
            try
            {
                return File.GetLastWriteTimeUtc(path);
            }
            catch
            {
                return null;
            }
        }

        private static string TryReadError(string body)
        {
            try
            {
                var parsed = Deserialize<ChatResponse>(body);
                return parsed?.Error?.Message;
            }
            catch
            {
                return null;
            }
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }
            return value.Substring(0, maxLength) + "…";
        }

        private static string Serialize<T>(T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static T Deserialize<T>(string json) where T : class
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return serializer.ReadObject(stream) as T;
            }
        }

        [DataContract]
        public sealed class ChatRequest
        {
            [DataMember(Name = "model")]
            public string Model { get; set; }

            [DataMember(Name = "messages")]
            public ChatMessage[] Messages { get; set; }

            [DataMember(Name = "temperature")]
            public double Temperature { get; set; }

            [DataMember(Name = "stream")]
            public bool Stream { get; set; }
        }

        [DataContract]
        public sealed class ChatMessage
        {
            [DataMember(Name = "role")]
            public string Role { get; set; }

            [DataMember(Name = "content")]
            public string Content { get; set; }
        }

        [DataContract]
        public sealed class ChatResponse
        {
            [DataMember(Name = "choices")]
            public ChatChoice[] Choices { get; set; }

            [DataMember(Name = "error")]
            public ApiError Error { get; set; }
        }

        [DataContract]
        public sealed class ChatChoice
        {
            [DataMember(Name = "message")]
            public ChatMessage Message { get; set; }
        }

        [DataContract]
        public sealed class ApiError
        {
            [DataMember(Name = "message")]
            public string Message { get; set; }
        }

        [DataContract]
        public sealed class LocateResponse
        {
            [DataMember(Name = "candidates")]
            public LocateCandidate[] Candidates { get; set; }
        }

        [DataContract]
        public sealed class LocateCandidate
        {
            [DataMember(Name = "path")]
            public string Path { get; set; }

            [DataMember(Name = "kind")]
            public string Kind { get; set; }

            [DataMember(Name = "confidence")]
            public double Confidence { get; set; }

            [DataMember(Name = "reason")]
            public string Reason { get; set; }
        }
    }
}
