using System;

namespace SharedLibraryCore.Exceptions
{
    public class ConfigurationException : Exception
    {
        public ConfigurationException(string message) : base(message)
        {
        }

        public string[] Errors { get; set; }
        public string ConfigurationFileName { get; set; }
    }
}