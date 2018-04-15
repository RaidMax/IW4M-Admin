using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Objects;

namespace Application.EventParsers
{
    class IW4EventParser : IEventParser
    {
        public GameEvent GetEvent(Server server, string logLine)
        {
            string[] lineSplit = logLine.Split(';');
            string cleanedEventLine = Regex.Replace(lineSplit[0], @"[0-9]+:[0-9]+\ ", "");

            if (cleanedEventLine[0] == 'K')
            {
                if (!server.CustomCallback)
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
            }

            if (lineSplit[0].Substring(lineSplit[0].Length - 3).Trim() == "say")
            {
                return new GameEvent()
                {
                    Type = GameEvent.EventType.Say,
                    Data = lineSplit[4].Replace("\x15", ""),
                    Origin = server.GetPlayersAsList().First(c => c.ClientNumber == Utilities.ClientIdFromString(lineSplit, 2)),
                    Owner = server,
                    Message = lineSplit[4]
                };
            }

            if (cleanedEventLine.Contains("ScriptKill"))
            {
                return new GameEvent()
                {
                    Type = GameEvent.EventType.Script,
                    Data = logLine,
                    Origin = server.GetPlayersAsList().First(c => c.NetworkId == lineSplit[1].ConvertLong()),
                    Target = server.GetPlayersAsList().First(c => c.NetworkId == lineSplit[2].ConvertLong()),
                    Owner = server
                };
            }

            if (cleanedEventLine.Contains("ExitLevel"))
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

            if (cleanedEventLine.Contains("InitGame"))
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

        // other parsers can derive from this parser so we make it virtual
        public virtual string GetGameDir() => "userraw";
    }
}
