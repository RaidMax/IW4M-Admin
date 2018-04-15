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
            string[] lineSplit = logLine.Split(';');
            string cleanedEventName = Regex.Replace(lineSplit[0], @" +[0-9]+:[0-9]+ +", "");

            if (cleanedEventName[0] == 'K')
            {
                return new GameEvent()
                {
                    Type = GameEvent.EventType.Script,
                    Data = logLine,
                    Origin = server.GetPlayersAsList().First(c => c.ClientNumber == Utilities.ClientIdFromString(lineSplit, 6)),
                    Target = server.GetPlayersAsList().First(c => c.ClientNumber == Utilities.ClientIdFromString(lineSplit, 2)),
                    Owner = server
                };
            }

            if (cleanedEventName == "say")
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

            if (cleanedEventName.Contains("ShutdownGame"))
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

            if (cleanedEventName.Contains("InitGame"))
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
