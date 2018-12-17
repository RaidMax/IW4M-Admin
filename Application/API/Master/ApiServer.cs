using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace IW4MAdmin.Application.API.Master
{
    public class ApiServer
    {
        [JsonProperty("id")]
        public long Id { get; set; }
        [JsonProperty("ip")]
        public string IPAddress { get; set; }
        [JsonProperty("port")]
        public short Port { get; set; }
        [JsonProperty("version")]
        public string Version { get; set; }
        [JsonProperty("gametype")]
        public string Gametype { get; set; }
        [JsonProperty("map")]
        public string Map { get; set; }
        [JsonProperty("game")]
        public string Game { get; set; }
        [JsonProperty("hostname")]
        public string Hostname { get; set; }
        [JsonProperty("clientnum")]
        public int ClientNum { get; set; }
        [JsonProperty("maxclientnum")]
        public int MaxClientNum { get; set; }
    }
}
