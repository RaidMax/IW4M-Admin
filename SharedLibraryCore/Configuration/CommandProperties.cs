using Newtonsoft.Json.Converters;
using System.Text.Json.Serialization;
using static SharedLibraryCore.Database.Models.EFClient;

namespace SharedLibraryCore.Configuration
{
    /// <summary>
    /// Config driven command properties
    /// </summary>
    public class CommandProperties
    {
        /// <summary>
        /// Specifies the command name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Alias of this command
        /// </summary>
        public string Alias { get; set; }

        /// <summary>
        /// Specifies the minimum permission level needed to execute the 
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public Permission MinimumPermission { get; set; }

        /// <summary>
        /// Indicates if the command can be run by another user (impersonation)
        /// </summary>
        public bool AllowImpersonation { get; set; }
    }
}
