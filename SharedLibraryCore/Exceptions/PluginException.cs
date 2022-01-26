using System;

namespace SharedLibraryCore.Exceptions
{
    public class PluginException : Exception
    {
        public PluginException(string message) : base(message)
        {
        }

        public string PluginFile { get; set; }
    }
}