using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using System;
using System.Text.RegularExpressions;

namespace IW4MAdmin.Application.EventParsers
{
    class IW5EventParser : IW4EventParser
    {
        public override string GetGameDir()
        {
            return "logs";
        }

        public override GameEvent GetEvent(Server server, string logLine)
        {
            string cleanedEventLine = Regex.Replace(logLine, @"[0-9]+:[0-9]+\ ", "").Trim();

            if (cleanedEventLine.Contains("J;"))
            {
                string[] lineSplit = cleanedEventLine.Split(';');

                int clientNum = Int32.Parse(lineSplit[2]);

                var player = new EFClient()
                {
                    NetworkId = lineSplit[1].ConvertLong(),
                    ClientNumber = clientNum,
                    CurrentAlias = new EFAlias()
                    {
                        Active = false,
                        Name = lineSplit[3]
                    }
                };

                return new GameEvent()
                {
                    Type = GameEvent.EventType.PreConnect,
                    Origin = new EFClient()
                    {
                        ClientId = 1
                    },
                    Target = new EFClient()
                    {
                        ClientId = 1
                    },
                    Owner = server,
                    Extra = player
                };
            }

            else
            {
                return base.GetEvent(server, logLine);
            }
        }
    }
}
