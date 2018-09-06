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
        private const string SayRegex = @"(say|sayteam);(.{1,32});([0-9]+)(.*);(.*)";

        public virtual GameEvent GetEvent(Server server, string logLine)
        {
            logLine = Regex.Replace(logLine, @"([0-9]+:[0-9]+ |^[0-9]+ )", "").Trim();
            string[] lineSplit = logLine.Split(';');
            string eventType = lineSplit[0];

            if (eventType == "JoinTeam")
            {
                var origin = server.GetPlayersAsList().FirstOrDefault(c => c.NetworkId == lineSplit[1].ConvertLong());

                return new GameEvent()
                {
                    Type = GameEvent.EventType.JoinTeam,
                    Data = eventType,
                    Origin = origin,
                    Owner = server
                };
            }

            if (eventType == "say" || eventType == "sayteam")
            {
                var matchResult = Regex.Match(logLine, SayRegex);

                if (matchResult.Success)
                {
                    string message = matchResult.Groups[5].ToString()
                        .Replace("\x15", "")
                        .Trim();

                    var origin = server.GetPlayersAsList().First(c => c.ClientNumber == Utilities.ClientIdFromString(lineSplit, 2));

                    if (message[0] == '!' || message[0] == '@')
                    {
                        return new GameEvent()
                        {
                            Type = GameEvent.EventType.Command,
                            Data = message,
                            Origin = origin,
                            Owner = server,
                            Message = message
                        };
                    }

                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.Say,
                        Data = message,
                        Origin = origin,
                        Owner = server,
                        Message = message
                    };
                }
            }

            if (eventType == "K")
            {
                if (!server.CustomCallback)
                {
                    var origin = server.GetPlayersAsList().First(c => c.ClientNumber == Utilities.ClientIdFromString(lineSplit, 6));
                    var target = server.GetPlayersAsList().First(c => c.ClientNumber == Utilities.ClientIdFromString(lineSplit, 2));

                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.Kill,
                        Data = logLine,
                        Origin = origin,
                        Target = target,
                        Owner = server
                    };
                }
            }

            if (eventType == "ScriptKill")
            {
                var origin = server.GetPlayersAsList().First(c => c.NetworkId == lineSplit[1].ConvertLong());
                var target = server.GetPlayersAsList().First(c => c.NetworkId == lineSplit[2].ConvertLong());
                return new GameEvent()
                {
                    Type = GameEvent.EventType.ScriptKill,
                    Data = logLine,
                    Origin = origin,
                    Target = target,
                    Owner = server
                };
            }

            if (eventType == "ScriptDamage")
            {
                var origin = server.GetPlayersAsList().First(c => c.NetworkId == lineSplit[1].ConvertLong());
                var target = server.GetPlayersAsList().First(c => c.NetworkId == lineSplit[2].ConvertLong());

                return new GameEvent()
                {
                    Type = GameEvent.EventType.ScriptDamage,
                    Data = logLine,
                    Origin = origin,
                    Target = target,
                    Owner = server
                };
            }

            // damage
            if (eventType == "D")
            {
                if (!server.CustomCallback)
                {
                    if (Regex.Match(eventType, @"^(D);((?:bot[0-9]+)|(?:[A-Z]|[0-9])+);([0-9]+);(axis|allies);(.+);((?:[A-Z]|[0-9])+);([0-9]+);(axis|allies);(.+);((?:[0-9]+|[a-z]+|_)+);([0-9]+);((?:[A-Z]|_)+);((?:[a-z]|_)+)$").Success)
                    {
                        var origin = server.GetPlayersAsList().First(c => c.NetworkId == lineSplit[5].ConvertLong());
                        var target = server.GetPlayersAsList().First(c => c.NetworkId == lineSplit[1].ConvertLong());

                        return new GameEvent()
                        {
                            Type = GameEvent.EventType.Damage,
                            Data = eventType,
                            Origin = origin,
                            Target = target,
                            Owner = server
                        };
                    }
                }
            }

            // join
            if (eventType == "J")
            {
                var regexMatch = Regex.Match(logLine, @"^(J;)(.{1,32});([0-9]+);(.*)$");
                if (regexMatch.Success)
                {
                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.Join,
                        Data = logLine,
                        Owner = server,
                        Origin = new Player()
                        {
                            Name = regexMatch.Groups[4].ToString().StripColors(),
                            NetworkId = regexMatch.Groups[2].ToString().ConvertLong(),
                            ClientNumber = Convert.ToInt32(regexMatch.Groups[3].ToString()),
                            State = Player.ClientState.Connecting,
                            CurrentServer = server
                        }
                    };
                }
            }

            //if (eventType == "Q")
            //{
            //    var regexMatch = Regex.Match(logLine, @"^(Q;)(.{1,32});([0-9]+);(.*)$");
            //    if (regexMatch.Success)
            //    {
            //        return new GameEvent()
            //        {
            //            Type = GameEvent.EventType.Quit,
            //            Data = logLine,
            //            Owner = server,
            //            Origin = new Player()
            //            {
            //                Name = regexMatch.Groups[4].ToString().StripColors(),
            //                NetworkId = regexMatch.Groups[2].ToString().ConvertLong(),
            //                ClientNumber = Convert.ToInt32(regexMatch.Groups[3].ToString()),
            //                State = Player.ClientState.Connecting
            //            }
            //        };
            //    }
            //}

            if (eventType.Contains("ExitLevel"))
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

            if (eventType.Contains("InitGame"))
            {
                string dump = eventType.Replace("InitGame: ", "");

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
