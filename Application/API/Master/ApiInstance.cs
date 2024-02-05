using System.Collections.Generic;
using System.Text.Json.Serialization;
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
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// Indicates how long the instance has been running
        /// </summary>
        [JsonPropertyName("uptime")]
        public int Uptime { get; set; }

        /// <summary>
        /// Specifies the version of the instance
        /// </summary>
        [JsonPropertyName("version")]
        [JsonConverter(typeof(BuildNumberJsonConverter))]
        public BuildNumber Version { get; set; }

        /// <summary>
        /// List of servers the instance is monitoring
        /// </summary>
        [JsonPropertyName("servers")]
        public List<ApiServer> Servers { get; set; }
        
        /// <summary>
        /// Url IW4MAdmin is listening on
        /// </summary>
        [JsonPropertyName("webfront_url")]
        public string WebfrontUrl { get; set; }
    }
}
