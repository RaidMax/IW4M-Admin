using System;

namespace SharedLibraryCore.Helpers
{
    public sealed class TokenState
    {
        public DateTime RequestTime { get; set; } = DateTime.Now;
        public TimeSpan TokenDuration { get; set; }
        public string Token { get; set; }

        public string RemainingTime => Math.Round(-(DateTime.Now - RequestTime).Subtract(TokenDuration).TotalMinutes, 1)
            .ToString();
    }
}
