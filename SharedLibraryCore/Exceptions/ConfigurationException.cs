using System;

namespace SharedLibraryCore.Exceptions
{
    public class ConfigurationException : Exception
    {
        public string[] Errors { get; set; }
        public string ConfigurationFileName { get; set; }

        public ConfigurationException(string message) : base(message) { }
    }
}
