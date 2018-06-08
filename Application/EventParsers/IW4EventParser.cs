using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Objects;

namespace IW4MAdmin.Application.EventParsers
{
    class IW4EventParser : IEventParser
    {
        public virtual GameEvent GetEvent(Server server, string logLine)
        {
            string[] lineSplit = logLine.Split(';');
            string cleanedEventLine = Regex.Replace(lineSplit[0], @"([0-9]+:[0-9]+ |^[0-9]+ )", "").Trim();

            // kill
            if (cleanedEventLine[0] == 'K')
            {
                if (!server.CustomCallback)
                {
                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.Kill,
                        Data = logLine,
                        Origin = server.GetPlayersAsList().First(c => c.ClientNumber == Utilities.ClientIdFromString(lineSplit, 6)),
                        Target = server.GetPlayersAsList().First(c => c.ClientNumber == Utilities.ClientIdFromString(lineSplit, 2)),
                        Owner = server
                    };
                }
            }

            if (cleanedEventLine.Contains("JoinTeam"))
            {
                return new GameEvent()
                {
                    Type = GameEvent.EventType.JoinTeam,
                    Data = cleanedEventLine,
                    //Origin = server.GetPlayersAsList().First(c => c.NetworkId == lineSplit[1].ConvertLong()),
                    Owner = server
                };
            }

            if (cleanedEventLine == "say" || cleanedEventLine == "sayteam")
            {
                string message = lineSplit[4].Replace("\x15", "");

                if (message[0] == '!' || message[0] == '@')
                {
                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.Command,
                        Data = message,
                        Origin = server.GetPlayersAsList().First(c => c.ClientNumber == Utilities.ClientIdFromString(lineSplit, 2)),
                        Owner = server,
                        Message = message
                    };
                }

                return new GameEvent()
                {
                    Type = GameEvent.EventType.Say,
                    Data = message,
                    Origin = server.GetPlayersAsList().First(c => c.ClientNumber == Utilities.ClientIdFromString(lineSplit, 2)),
                    Owner = server,
                    Message = message
                };
            }

            if (cleanedEventLine.Contains("ScriptKill"))
            {
                return new GameEvent()
                {
                    Type = GameEvent.EventType.ScriptKill,
                    Data = logLine,
                    Origin = server.GetPlayersAsList().First(c => c.NetworkId == lineSplit[1].ConvertLong()),
                    Target = server.GetPlayersAsList().First(c => c.NetworkId == lineSplit[2].ConvertLong()),
                    Owner = server
                };
            }

            if (cleanedEventLine.Contains("ScriptDamage"))
            {
                return new GameEvent()
                {
                    Type = GameEvent.EventType.ScriptDamage,
                    Data = logLine,
                    Origin = server.GetPlayersAsList().First(c => c.NetworkId == lineSplit[1].ConvertLong()),
                    Target = server.GetPlayersAsList().First(c => c.NetworkId == lineSplit[2].ConvertLong()),
                    Owner = server
                };
            }

            // damage
            if (cleanedEventLine[0] == 'D')
            {
                if (Regex.Match(cleanedEventLine, @"^(D);((?:bot[0-9]+)|(?:[A-Z]|[0-9])+);([0-9]+);(axis|allies);(.+);((?:[A-Z]|[0-9])+);([0-9]+);(axis|allies);(.+);((?:[0-9]+|[a-z]+|_)+);([0-9]+);((?:[A-Z]|_)+);((?:[a-z]|_)+)$").Success)
                {
                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.Damage,
                        Data = cleanedEventLine,
                        Origin = server.GetPlayersAsList().First(c => c.NetworkId == lineSplit[5].ConvertLong()),
                        Target = server.GetPlayersAsList().First(c => c.NetworkId == lineSplit[1].ConvertLong()),
                        Owner = server
                    };
                }
            }

            // join
            if (cleanedEventLine[0] == 'J')
            {
                var regexMatch = Regex.Match(cleanedEventLine, @"^(J;)(.{4,32});([0-9]+);(.*)$");
                if (regexMatch.Success)
                {
                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.Join,
                        Data = cleanedEventLine,
                        Owner = server,
                        Origin = new Player()
                        {
                            Name = regexMatch.Groups[4].ToString(),
                            NetworkId = regexMatch.Groups[2].ToString().ConvertLong(),
                            ClientNumber = Convert.ToInt32(regexMatch.Groups[3].ToString())
                        }
                    };
                }
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

        // other parsers can derive from this parser so we make it virtual
        public virtual string GetGameDir() => "userraw";
    }
}
