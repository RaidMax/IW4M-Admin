using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IW4MAdmin.Application.EventParsers
{
    class IW5EventParser : IW4EventParser
    {
        public override string GetGameDir() => "logs";

        public override GameEvent GetEvent(Server server, string logLine)
        {
            string cleanedEventLine = Regex.Replace(logLine, @"[0-9]+:[0-9]+\ ", "").Trim();

            if (cleanedEventLine.Contains("J;"))
            {
                string[] lineSplit = cleanedEventLine.Split(';');

                int clientNum = Int32.Parse(lineSplit[2]);

                var player = new Player()
                {
                    NetworkId = lineSplit[1].ConvertLong(),
                    ClientNumber = clientNum,
                    Name = lineSplit[3]
                };

                return new GameEvent()
                {
                    Type = GameEvent.EventType.Connect,
                    Origin = new Player()
                    {
                        ClientId = 1
                    },
                    Target = new Player()
                    {
                        ClientId = 1
                    },
                    Owner = server,
                    Extra = player
                };
            }

            else
                return base.GetEvent(server, logLine);
        }
    }
}
