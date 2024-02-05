using System.Text.Json.Serialization;

namespace IW4MAdmin.Application.API.GameLogServer
{
    public class LogInfo
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        [JsonPropertyName("length")]
        public int Length { get; set; }
        [JsonPropertyName("data")]
        public string Data { get; set; }
        [JsonPropertyName("next_key")]
        public string NextKey { get; set; }
    }
}
