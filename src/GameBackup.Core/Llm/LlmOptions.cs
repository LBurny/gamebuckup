using System;

namespace GameBackup.Core.Llm
{
    public sealed class LlmOptions
    {
        public string Endpoint { get; set; }

        public string Model { get; set; }

        public string ApiKey { get; set; }

        public int TimeoutSeconds { get; set; } = 60;
    }

    public sealed class LlmTestResult
    {
        public bool Success { get; set; }

        public string Message { get; set; }

        public string Model { get; set; }

        public TimeSpan Latency { get; set; }
    }
}
