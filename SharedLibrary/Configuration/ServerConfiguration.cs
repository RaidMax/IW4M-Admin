using System.Collections.Generic;

namespace SharedLibrary.Configuration
{
    public class ServerConfiguration
    {
        public string IPAddress { get; set; }
        public short Port { get; set; }
        public string Password { get; set; }
        public List<string> Rules { get; set; }
        public List<string> AutoMessages { get; set; }
    }
}
