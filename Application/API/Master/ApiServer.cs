using System.Text.Json.Serialization;

namespace IW4MAdmin.Application.API.Master
{
    public class ApiServer
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
        [JsonPropertyName("ip")]
        public string IPAddress { get; set; }
        [JsonPropertyName("port")]
        public short Port { get; set; }
        [JsonPropertyName("version")]
        public string Version { get; set; }
        [JsonPropertyName("gametype")]
        public string Gametype { get; set; }
        [JsonPropertyName("map")]
        public string Map { get; set; }
        [JsonPropertyName("game")]
        public string Game { get; set; }
        [JsonPropertyName("hostname")]
        public string Hostname { get; set; }
        [JsonPropertyName("clientnum")]
        public int ClientNum { get; set; }
        [JsonPropertyName("maxclientnum")]
        public int MaxClientNum { get; set; }
    }
}
