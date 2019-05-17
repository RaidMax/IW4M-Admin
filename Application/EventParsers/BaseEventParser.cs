using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using static SharedLibraryCore.Server;

namespace IW4MAdmin.Application.EventParsers
{
    class BaseEventParser : IEventParser
    {
        public BaseEventParser()
        {
            Configuration = new DynamicEventParserConfiguration()
            {
                GameDirectory = "main",
            };

            Configuration.Say.Pattern = @"^(say|sayteam);(-?[A-Fa-f0-9_]{1,32});([0-9]+);(.+);(.*)$";
            Configuration.Say.AddMapping(ParserRegex.GroupType.EventType, 1);
            Configuration.Say.AddMapping(ParserRegex.GroupType.OriginNetworkId, 2);
            Configuration.Say.AddMapping(ParserRegex.GroupType.OriginClientNumber, 3);
            Configuration.Say.AddMapping(ParserRegex.GroupType.OriginName, 4);
            Configuration.Say.AddMapping(ParserRegex.GroupType.Message, 5);

            Configuration.Quit.Pattern = @"^(Q);(-?[A-Fa-f0-9_]{1,32}|bot[0-9]+);([0-9]+);(.*)$";
            Configuration.Quit.AddMapping(ParserRegex.GroupType.EventType, 1);
            Configuration.Quit.AddMapping(ParserRegex.GroupType.OriginNetworkId, 2);
            Configuration.Quit.AddMapping(ParserRegex.GroupType.OriginClientNumber, 3);
            Configuration.Quit.AddMapping(ParserRegex.GroupType.OriginName, 4);

            Configuration.Join.Pattern = @"^(J);(-?[A-Fa-f0-9_]{1,32}|bot[0-9]+);([0-9]+);(.*)$";
            Configuration.Join.AddMapping(ParserRegex.GroupType.EventType, 1);
            Configuration.Join.AddMapping(ParserRegex.GroupType.OriginNetworkId, 2);
            Configuration.Join.AddMapping(ParserRegex.GroupType.OriginClientNumber, 3);
            Configuration.Join.AddMapping(ParserRegex.GroupType.OriginName, 4);

            Configuration.Damage.Pattern = @"^(D);(-?[A-Fa-f0-9_]{1,32}|bot[0-9]+);(-?[0-9]+);(axis|allies|world);(.{1,24});(-?[A-Fa-f0-9_]{1,32}|bot[0-9]+)?;-?([0-9]+);(axis|allies|world);(.{1,24})?;((?:[0-9]+|[a-z]+|_)+);([0-9]+);((?:[A-Z]|_)+);((?:[a-z]|_)+)$";
            Configuration.Damage.AddMapping(ParserRegex.GroupType.EventType, 1);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.TargetNetworkId, 2);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.TargetClientNumber, 3);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.TargetTeam, 4);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.TargetName, 5);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.OriginNetworkId, 6);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.OriginClientNumber, 7);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.OriginTeam, 8);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.OriginName, 9);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.Weapon, 10);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.Damage, 11);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.MeansOfDeath, 12);
            Configuration.Damage.AddMapping(ParserRegex.GroupType.HitLocation, 13);

            Configuration.Kill.Pattern = @"^(K);(-?[A-Fa-f0-9_]{1,32}|bot[0-9]+);(-?[0-9]+);(axis|allies|world);(.{1,24});(-?[A-Fa-f0-9_]{1,32}|bot[0-9]+)?;-?([0-9]+);(axis|allies|world);(.{1,24})?;((?:[0-9]+|[a-z]+|_)+);([0-9]+);((?:[A-Z]|_)+);((?:[a-z]|_)+)$";
            Configuration.Kill.AddMapping(ParserRegex.GroupType.EventType, 1);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.TargetNetworkId, 2);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.TargetClientNumber, 3);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.TargetTeam, 4);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.TargetName, 5);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.OriginNetworkId, 6);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.OriginClientNumber, 7);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.OriginTeam, 8);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.OriginName, 9);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.Weapon, 10);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.Damage, 11);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.MeansOfDeath, 12);
            Configuration.Kill.AddMapping(ParserRegex.GroupType.HitLocation, 13);
        }

        public IEventParserConfiguration Configuration { get; set; }

        public string Version { get; set; } = "CoD";

        public Game GameName { get; set; } = Game.COD;

        public string URLProtocolFormat { get; set; } = "CoD://{{ip}}:{{port}}";

        public virtual GameEvent GenerateGameEvent(string logLine)
        {
            logLine = Regex.Replace(logLine, @"([0-9]+:[0-9]+ |^[0-9]+ )", "").Trim();
            string[] lineSplit = logLine.Split(';');
            string eventType = lineSplit[0];

            if (eventType == "say" || eventType == "sayteam")
            {
                var matchResult = Regex.Match(logLine, Configuration.Say.Pattern);

                if (matchResult.Success)
                {
                    string message = matchResult
                        .Groups[Configuration.Say.GroupMapping[ParserRegex.GroupType.Message]]
                        .ToString()
                        .Replace("\x15", "")
                        .Trim();

                    if (message.Length > 0)
                    {
                        long originId = matchResult.Groups[Configuration.Say.GroupMapping[ParserRegex.GroupType.OriginNetworkId]].ToString().ConvertGuidToLong();

                        if (message[0] == '!' || message[0] == '@')
                        {
                            return new GameEvent()
                            {
                                Type = GameEvent.EventType.Command,
                                Data = message,
                                Origin = new EFClient() { NetworkId = originId },
                                Message = message
                            };
                        }

                        return new GameEvent()
                        {
                            Type = GameEvent.EventType.Say,
                            Data = message,
                            Origin = new EFClient() { NetworkId = originId },
                            Message = message
                        };
                    }
                }
            }

            if (eventType == "K")
            {

                var match = Regex.Match(logLine, Configuration.Kill.Pattern);

                if (match.Success)
                {
                    long originId = match.Groups[Configuration.Kill.GroupMapping[ParserRegex.GroupType.OriginNetworkId]].Value.ToString().ConvertGuidToLong(1);
                    long targetId = match.Groups[Configuration.Kill.GroupMapping[ParserRegex.GroupType.TargetNetworkId]].Value.ToString().ConvertGuidToLong();

                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.Kill,
                        Data = logLine,
                        Origin = new EFClient() { NetworkId = originId },
                        Target = new EFClient() { NetworkId = targetId },
                    };
                }
            }

            if (eventType == "D")
            {
                    var regexMatch = Regex.Match(logLine, Configuration.Damage.Pattern);

                    if (regexMatch.Success)
                    {
                        long originId = regexMatch.Groups[Configuration.Damage.GroupMapping[ParserRegex.GroupType.OriginNetworkId]].ToString().ConvertGuidToLong(1);
                        long targetId = regexMatch.Groups[Configuration.Damage.GroupMapping[ParserRegex.GroupType.TargetNetworkId]].ToString().ConvertGuidToLong();

                        return new GameEvent()
                        {
                            Type = GameEvent.EventType.Damage,
                            Data = logLine,
                            Origin = new EFClient() { NetworkId = originId },
                            Target = new EFClient() { NetworkId = targetId }
                        };
                    }
            }

            if (eventType == "J")
            {
                var regexMatch = Regex.Match(logLine, Configuration.Join.Pattern);

                if (regexMatch.Success)
                {
                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.PreConnect,
                        Data = logLine,
                        Origin = new EFClient()
                        {
                            CurrentAlias = new EFAlias()
                            {
                                Name = regexMatch.Groups[Configuration.Join.GroupMapping[ParserRegex.GroupType.OriginName]].ToString().StripColors(),
                            },
                            NetworkId = regexMatch.Groups[Configuration.Join.GroupMapping[ParserRegex.GroupType.OriginNetworkId]].ToString().ConvertGuidToLong(),
                            ClientNumber = Convert.ToInt32(regexMatch.Groups[Configuration.Join.GroupMapping[ParserRegex.GroupType.OriginClientNumber]].ToString()),
                            State = EFClient.ClientState.Connecting,
                        }
                    };
                }
            }

            if (eventType == "Q")
            {
                var regexMatch = Regex.Match(logLine, Configuration.Quit.Pattern);
                if (regexMatch.Success)
                {
                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.PreDisconnect,
                        Data = logLine,
                        Origin = new EFClient()
                        {
                            CurrentAlias = new EFAlias()
                            {
                                Name = regexMatch.Groups[Configuration.Quit.GroupMapping[ParserRegex.GroupType.OriginName]].ToString().StripColors()
                            },
                            NetworkId = regexMatch.Groups[Configuration.Quit.GroupMapping[ParserRegex.GroupType.OriginNetworkId]].ToString().ConvertGuidToLong(),
                            ClientNumber = Convert.ToInt32(regexMatch.Groups[Configuration.Quit.GroupMapping[ParserRegex.GroupType.OriginClientNumber]].ToString()),
                            State = EFClient.ClientState.Disconnecting
                        }
                    };
                }
            }

            if (eventType.Contains("ExitLevel"))
            {
                return new GameEvent()
                {
                    Type = GameEvent.EventType.MapEnd,
                    Data = lineSplit[0],
                    Origin = Utilities.IW4MAdminClient(),
                    Target = Utilities.IW4MAdminClient(),
                };
            }

            if (eventType.Contains("InitGame"))
            {
                string dump = eventType.Replace("InitGame: ", "");

                return new GameEvent()
                {
                    Type = GameEvent.EventType.MapChange,
                    Data = lineSplit[0],
                    Origin = Utilities.IW4MAdminClient(),
                    Target = Utilities.IW4MAdminClient(),
                    Extra = dump.DictionaryFromKeyValue()
                };
            }

            // this is a custom event printed out by _customcallbacks.gsc (used for team balance)
            if (eventType == "JoinTeam")
            {
                return new GameEvent()
                {
                    Type = GameEvent.EventType.JoinTeam,
                    Data = logLine,
                    Origin = new EFClient() { NetworkId = lineSplit[1].ConvertGuidToLong() }
                };
            }

            // this is a custom event printed out by _customcallbacks.gsc (used for anticheat)
            if (eventType == "ScriptKill")
            {
                long originId = lineSplit[1].ConvertGuidToLong(1);
                long targetId = lineSplit[2].ConvertGuidToLong();

                return new GameEvent()
                {
                    Type = GameEvent.EventType.ScriptKill,
                    Data = logLine,
                    Origin = new EFClient() { NetworkId = originId },
                    Target = new EFClient() { NetworkId = targetId }
                };
            }

            // this is a custom event printed out by _customcallbacks.gsc (used for anticheat)
            if (eventType == "ScriptDamage")
            {
                long originId = lineSplit[1].ConvertGuidToLong(1);
                long targetId = lineSplit[2].ConvertGuidToLong();

                return new GameEvent()
                {
                    Type = GameEvent.EventType.ScriptDamage,
                    Data = logLine,
                    Origin = new EFClient() { NetworkId = originId },
                    Target = new EFClient() { NetworkId = targetId }
                };
            }

            return new GameEvent()
            {
                Type = GameEvent.EventType.Unknown,
                Origin = Utilities.IW4MAdminClient(),
                Target = Utilities.IW4MAdminClient()
            };
        }
    }
}
