using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.RCon
{
    public class CommandPrefix
    {
        public string Tell { get; set; }
        public string Say { get; set; }
        public string Set { get; set; }
        public string Kick { get; set; }
        public string Ban { get; set; }
        public string Unban { get; set; }
        public string TempBan { get; set; }
    }
}
