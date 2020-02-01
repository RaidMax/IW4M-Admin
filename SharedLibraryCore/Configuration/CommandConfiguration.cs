using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;

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

        public IBaseConfiguration Generate()
        {
            throw new NotImplementedException();
        }

        public string Name() => nameof(CommandConfiguration);
    }
}
