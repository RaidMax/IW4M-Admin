using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SharedLibraryCore.Dtos
{
    public class ClientHistoryInfo
    {
        public long ServerId { get; set; }
        public List<ClientCountSnapshot> ClientCounts { get; set; } = new();
    }

    public class ClientCountSnapshot
    {
        [JsonIgnore]
        public DateTime Time { get; set; }
        [JsonPropertyName("ts")]
        public string TimeString => Time.ToString("yyyy-MM-ddTHH:mm:ssZ");
        [JsonPropertyName("cc")]
        public int ClientCount { get; set; }
        [JsonPropertyName("ci")]
        public bool ConnectionInterrupted { get;set; }
        [JsonIgnore]
        public string Map { get; set; }
        [JsonPropertyName("ma")]
        public string MapAlias { get; set; }
    }
}
