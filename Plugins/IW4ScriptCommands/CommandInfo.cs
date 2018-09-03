using System;
using System.Collections.Generic;
using System.Text;

namespace IW4ScriptCommands
{
    class CommandInfo
    {
        public string Command { get; set; }
        public int ClientNumber { get; set; }
        public List<string> CommandArguments { get; set; } = new List<string>();
        public override string ToString() => $"{Command};{ClientNumber},{string.Join(',', CommandArguments)}";
    }
}
