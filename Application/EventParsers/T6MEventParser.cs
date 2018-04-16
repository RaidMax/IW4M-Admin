using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Objects;

namespace Application.EventParsers
{
    class T6MEventParser : IEventParser
    {
        public GameEvent GetEvent(Server server, string logLine)
        {
            string cleanedLogLine = Regex.Replace(logLine, @"^ *[0-9]+:[0-9]+ *", "");
            string[] lineSplit = cleanedLogLine.Split(';');

            if (lineSplit[0][0] == 'K')
            {
                return new GameEvent()
                {
                    Type = GameEvent.EventType.Script,
                    Data = cleanedLogLine,
                    Origin = server.GetPlayersAsList().First(c => c.ClientNumber == Utilities.ClientIdFromString(lineSplit, 6)),
                    Target = server.GetPlayersAsList().First(c => c.ClientNumber == Utilities.ClientIdFromString(lineSplit, 2)),
                    Owner = server
                };
            }

            if (lineSplit[0][0] == 'D')
            {
                return new GameEvent()
                {
                    Type = GameEvent.EventType.Damage,
                    Data = cleanedLogLine,
                    Origin = server.GetPlayersAsList().First(c => c.ClientNumber == Utilities.ClientIdFromString(lineSplit, 6)),
                    Target = server.GetPlayersAsList().First(c => c.ClientNumber == Utilities.ClientIdFromString(lineSplit, 2)),
                    Owner = server
                };
            }

            if (lineSplit[0] == "say")
            {
                return new GameEvent()
                {
                    Type = GameEvent.EventType.Say,
                    Data = lineSplit[4],
                    Origin = server.GetPlayersAsList().First(c => c.ClientNumber == Utilities.ClientIdFromString(lineSplit, 2)),
                    Owner = server,
                    Message = lineSplit[4]
                };
            }

            if (lineSplit[0].Contains("ShutdownGame"))
            {
                return new GameEvent()
                {
                    Type = GameEvent.EventType.MapEnd,
                    Data = lineSplit[0],
                    Origin = new Player()
                    {
                        ClientId = 1
                    },
                    Target = new Player()
                    {
                        ClientId = 1
                    },
                    Owner = server
                };
            }

            if (lineSplit[0].Contains("InitGame"))
            {
                return new GameEvent()
                {
                    Type = GameEvent.EventType.MapChange,
                    Data = lineSplit[0],
                    Origin = new Player()
                    {
                        ClientId = 1
                    },
                    Target = new Player()
                    {
                        ClientId = 1
                    },
                    Owner = server
                };
            }

            return new GameEvent()
            {
                Type = GameEvent.EventType.Unknown,
                Origin = new Player()
                {
                    ClientId = 1
                },
                Target = new Player()
                {
                    ClientId = 1
                },
                Owner = server
            };
        }

        public string GetGameDir() => $"t6r{Path.DirectorySeparatorChar}data";
    }
}
