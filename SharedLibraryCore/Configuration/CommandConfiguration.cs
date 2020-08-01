using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SharedLibraryCore.Configuration
{
    /// <summary>
    /// Basic command configuration
    /// </summary>
    public class CommandConfiguration : IBaseConfiguration
    {
        /// <summary>
        /// Dict of command class names mapped to configurable properties
        /// </summary>
        public Dictionary<string, CommandProperties> Commands { get; set; } = new Dictionary<string, CommandProperties>();

        /// <summary>
        /// prefix indicated the chat message is a command
        /// </summary>
        [JsonIgnore]
        public string CommandPrefix { get; set; }

        /// <summary>
        /// prefix indicating that the chat message is a broadcast command
        /// </summary>
        [JsonIgnore]
        public string BroadcastCommandPrefix { get; set; }

        public IBaseConfiguration Generate()
        {
            throw new NotImplementedException();
        }

        public string Name() => nameof(CommandConfiguration);
    }
}
