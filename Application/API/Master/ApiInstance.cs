using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using RestEase;

namespace IW4MAdmin.Application.API.Master
{
    public class ApiInstance
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("uptime")]
        public int Uptime { get; set; }
        [JsonProperty("version")]
        public float Version { get; set; }
        [JsonProperty("servers")]
        public List<ApiServer> Servers { get; set; }
    }
}
