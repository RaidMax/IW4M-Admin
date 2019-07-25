using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace IW4MAdmin.Application.API.GameLogServer
{
    public class LogInfo
    {
        [JsonProperty("success")]
        public bool Success { get; set; }
        [JsonProperty("length")]
        public int Length { get; set; }
        [JsonProperty("data")]
        public string Data { get; set; }
        [JsonProperty("next_key")]
        public string NextKey { get; set; }
    }
}
