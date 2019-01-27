using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Interfaces
{
    public interface IEventParserConfiguration
    {
        string GameDirectory { get; set; }
        string SayRegex { get; set; }
        string JoinRegex { get; set; }
        string QuitRegex { get; set; }
        string KillRegex { get; set; }
        string DamageRegex { get; set; }
    }
}
