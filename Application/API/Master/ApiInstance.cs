using System.Collections.Generic;
using Newtonsoft.Json;
using SharedLibraryCore.Helpers;

namespace IW4MAdmin.Application.API.Master
{
    /// <summary>
    /// Defines the structure of the IW4MAdmin instance for the master API
    /// </summary>
    public class ApiInstance
    {
        /// <summary>
        /// Unique ID of the instance
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>
        /// Indicates how long the instance has been running
        /// </summary>
        [JsonProperty("uptime")]
        public int Uptime { get; set; }

        /// <summary>
        /// Specifices the version of the instance
        /// </summary>
        [JsonProperty("version")]
        [JsonConverter(typeof(BuildNumberJsonConverter))]
        public BuildNumber Version { get; set; }

        /// <summary>
        /// List of servers the instance is monitoring
        /// </summary>
        [JsonProperty("servers")]
        public List<ApiServer> Servers { get; set; }
    }
}
