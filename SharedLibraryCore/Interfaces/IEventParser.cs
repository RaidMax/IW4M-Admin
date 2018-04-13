using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Interfaces
{
    public interface IEventParser
    {
        GameEvent GetEvent(Server server, string logLine);
    }
}
