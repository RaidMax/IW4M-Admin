using System.Collections.Generic;
using static SharedLibraryCore.Server;

namespace SharedLibraryCore.Configuration
{
    public class QuickMessageConfiguration
    {
        public Game Game { get; set; }
        public Dictionary<string, string> Messages { get; set; }
    }
}