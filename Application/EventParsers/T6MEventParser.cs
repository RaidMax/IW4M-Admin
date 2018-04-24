using System;
using System.Collections.Generic;
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
            string cleanedEventLine = Regex.Replace(logLine, @"^ *[0-9]+:[0-9]+ *", "").Trim();
            string[] lineSplit = cleanedEventLine.Split(';');

            if (lineSplit[0][0] == 'K')
            {
                return new GameEvent()
                {
                    Type = GameEvent.EventType.Script,
                    Data = cleanedEventLine,
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
                    Data = cleanedEventLine,
                    Origin = server.GetPlayersAsList().First(c => c.ClientNumber == Utilities.ClientIdFromString(lineSplit, 6)),
                    Target = server.GetPlayersAsList().First(c => c.ClientNumber == Utilities.ClientIdFromString(lineSplit, 2)),
                    Owner = server
                };
            }

            if (lineSplit[0] == "say" || lineSplit[0] == "sayteam")
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

            if (lineSplit[0].Contains("ExitLevel"))
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
                string dump = cleanedEventLine.Replace("InitGame: ", "");

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
                    Owner = server,
                    Extra = dump.DictionaryFromKeyValue()
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
