using System;

namespace SharedLibraryCore.Exceptions
{
    public class ConfigurationException : Exception
    {
        public string[] Errors { get; set; }

        public ConfigurationException(string message) : base(message) { }
    }
}
