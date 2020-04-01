using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System;
using System.Linq;
using static SharedLibraryCore.Server;

namespace IW4MAdmin.Application.EventParsers
{
    public class BaseEventParser : IEventParser
    {
        public BaseEventParser(IParserRegexFactory parserRegexFactory)
        {
            Configuration = new DynamicEventParserConfiguration(parserRegexFactory)
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

            Configuration.Damage.Pattern = @"^(D);(-?[A-Fa-f0-9_]{1,32}|bot[0-9]+);(-?[0-9]+);(axis|allies|world)?;([^;]{1,24});(-?[A-Fa-f0-9_]{1,32}|bot[0-9]+)?;-?([0-9]+);(axis|allies|world)?;([^;]{1,24})?;((?:[0-9]+|[a-z]+|_|\+)+);([0-9]+);((?:[A-Z]|_)+);((?:[a-z]|_)+)$";
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

            Configuration.Kill.Pattern = @"^(K);(-?[A-Fa-f0-9_]{1,32}|bot[0-9]+);(-?[0-9]+);(axis|allies|world)?;([^;]{1,24});(-?[A-Fa-f0-9_]{1,32}|bot[0-9]+)?;-?([0-9]+);(axis|allies|world)?;([^;]{1,24})?;((?:[0-9]+|[a-z]+|_|\+)+);([0-9]+);((?:[A-Z]|_)+);((?:[a-z]|_)+)$";
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

            Configuration.Time.Pattern = @"^ *(([0-9]+):([0-9]+) |^[0-9]+ )";
        }

        public IEventParserConfiguration Configuration { get; set; }

        public string Version { get; set; } = "CoD";

        public Game GameName { get; set; } = Game.COD;

        public string URLProtocolFormat { get; set; } = "CoD://{{ip}}:{{port}}";

        public string Name { get; set; } = "Call of Duty";

        public virtual GameEvent GenerateGameEvent(string logLine)
        {
            var timeMatch = Configuration.Time.PatternMatcher.Match(logLine);
            int gameTime = 0;

            if (timeMatch.Success)
            {
                gameTime = timeMatch
                    .Values
                    .Skip(2)
                    // this converts the timestamp into seconds passed
                    .Select((_value, index) => int.Parse(_value.ToString()) * (index == 0 ? 60 : 1))
                    .Sum();
                // we want to strip the time from the log line
                logLine = logLine.Substring(timeMatch.Values.First().Length);
            }

            string[] lineSplit = logLine.Split(';');
            string eventType = lineSplit[0];

            if (eventType == "say" || eventType == "sayteam")
            {
                var matchResult = Configuration.Say.PatternMatcher.Match(logLine);

                if (matchResult.Success)
                {
                    string message = matchResult.Values[Configuration.Say.GroupMapping[ParserRegex.GroupType.Message]]
                        .ToString()
                        .Replace("\x15", "")
                        .Trim();

                    if (message.Length > 0)
                    {
                        long originId = matchResult.Values[Configuration.Say.GroupMapping[ParserRegex.GroupType.OriginNetworkId]].ToString().ConvertGuidToLong(Configuration.GuidNumberStyle);
                        int clientNumber = int.Parse(matchResult.Values[Configuration.Say.GroupMapping[ParserRegex.GroupType.OriginClientNumber]]);

                        // todo: these need to defined outside of here
                        if (message[0] == '!' || message[0] == '@')
                        {
                            return new GameEvent()
                            {
                                Type = GameEvent.EventType.Command,
                                Data = message,
                                Origin = new EFClient() { NetworkId = originId, ClientNumber = clientNumber },
                                Message = message,
                                Extra = logLine,
                                RequiredEntity = GameEvent.EventRequiredEntity.Origin,
                                GameTime = gameTime
                            };
                        }

                        return new GameEvent()
                        {
                            Type = GameEvent.EventType.Say,
                            Data = message,
                            Origin = new EFClient() { NetworkId = originId, ClientNumber = clientNumber },
                            Message = message,
                            Extra = logLine,
                            RequiredEntity = GameEvent.EventRequiredEntity.Origin,
                            GameTime = gameTime
                        };
                    }
                }
            }

            if (eventType == "K")
            {
                var match = Configuration.Kill.PatternMatcher.Match(logLine);

                if (match.Success)
                {
                    long originId = match.Values[Configuration.Kill.GroupMapping[ParserRegex.GroupType.OriginNetworkId]].ToString().ConvertGuidToLong(Configuration.GuidNumberStyle, 1);
                    long targetId = match.Values[Configuration.Kill.GroupMapping[ParserRegex.GroupType.TargetNetworkId]].ToString().ConvertGuidToLong(Configuration.GuidNumberStyle, 1);
                    int originClientNumber = int.Parse(match.Values[Configuration.Kill.GroupMapping[ParserRegex.GroupType.OriginClientNumber]]);
                    int targetClientNumber = int.Parse(match.Values[Configuration.Kill.GroupMapping[ParserRegex.GroupType.TargetClientNumber]]);

                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.Kill,
                        Data = logLine,
                        Origin = new EFClient() { NetworkId = originId, ClientNumber = originClientNumber },
                        Target = new EFClient() { NetworkId = targetId, ClientNumber = targetClientNumber },
                        RequiredEntity = GameEvent.EventRequiredEntity.Origin | GameEvent.EventRequiredEntity.Target,
                        GameTime = gameTime
                    };
                }
            }

            if (eventType == "D")
            {
                var match = Configuration.Damage.PatternMatcher.Match(logLine);

                if (match.Success)
                {
                    long originId = match.Values[Configuration.Damage.GroupMapping[ParserRegex.GroupType.OriginNetworkId]].ToString().ConvertGuidToLong(Configuration.GuidNumberStyle, 1);
                    long targetId = match.Values[Configuration.Damage.GroupMapping[ParserRegex.GroupType.TargetNetworkId]].ToString().ConvertGuidToLong(Configuration.GuidNumberStyle, 1);
                    int originClientNumber = int.Parse(match.Values[Configuration.Damage.GroupMapping[ParserRegex.GroupType.OriginClientNumber]]);
                    int targetClientNumber = int.Parse(match.Values[Configuration.Damage.GroupMapping[ParserRegex.GroupType.TargetClientNumber]]);

                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.Damage,
                        Data = logLine,
                        Origin = new EFClient() { NetworkId = originId, ClientNumber = originClientNumber },
                        Target = new EFClient() { NetworkId = targetId, ClientNumber = targetClientNumber },
                        RequiredEntity = GameEvent.EventRequiredEntity.Origin | GameEvent.EventRequiredEntity.Target,
                        GameTime = gameTime
                    };
                }
            }

            if (eventType == "J")
            {
                var match = Configuration.Join.PatternMatcher.Match(logLine);

                if (match.Success)
                {
                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.PreConnect,
                        Data = logLine,
                        Origin = new EFClient()
                        {
                            CurrentAlias = new EFAlias()
                            {
                                Name = match.Values[Configuration.Join.GroupMapping[ParserRegex.GroupType.OriginName]].ToString().TrimNewLine(),
                            },
                            NetworkId = match.Values[Configuration.Join.GroupMapping[ParserRegex.GroupType.OriginNetworkId]].ToString().ConvertGuidToLong(Configuration.GuidNumberStyle),
                            ClientNumber = Convert.ToInt32(match.Values[Configuration.Join.GroupMapping[ParserRegex.GroupType.OriginClientNumber]].ToString()),
                            State = EFClient.ClientState.Connecting,
                        },
                        RequiredEntity = GameEvent.EventRequiredEntity.None,
                        IsBlocking = true,
                        GameTime = gameTime
                    };
                }
            }

            if (eventType == "Q")
            {
                var match = Configuration.Quit.PatternMatcher.Match(logLine);

                if (match.Success)
                {
                    return new GameEvent()
                    {
                        Type = GameEvent.EventType.PreDisconnect,
                        Data = logLine,
                        Origin = new EFClient()
                        {
                            CurrentAlias = new EFAlias()
                            {
                                Name = match.Values[Configuration.Quit.GroupMapping[ParserRegex.GroupType.OriginName]].ToString().TrimNewLine()
                            },
                            NetworkId = match.Values[Configuration.Quit.GroupMapping[ParserRegex.GroupType.OriginNetworkId]].ToString().ConvertGuidToLong(Configuration.GuidNumberStyle),
                            ClientNumber = Convert.ToInt32(match.Values[Configuration.Quit.GroupMapping[ParserRegex.GroupType.OriginClientNumber]].ToString()),
                            State = EFClient.ClientState.Disconnecting
                        },
                        RequiredEntity = GameEvent.EventRequiredEntity.None,
                        IsBlocking = true,
                        GameTime = gameTime
                    };
                }
            }

            if (eventType.Contains("ExitLevel"))
            {
                return new GameEvent()
                {
                    Type = GameEvent.EventType.MapEnd,
                    Data = logLine,
                    Origin = Utilities.IW4MAdminClient(),
                    Target = Utilities.IW4MAdminClient(),
                    RequiredEntity = GameEvent.EventRequiredEntity.None,
                    GameTime = gameTime
                };
            }

            if (eventType.Contains("InitGame"))
            {
                string dump = eventType.Replace("InitGame: ", "");

                return new GameEvent()
                {
                    Type = GameEvent.EventType.MapChange,
                    Data = logLine,
                    Origin = Utilities.IW4MAdminClient(),
                    Target = Utilities.IW4MAdminClient(),
                    Extra = dump.DictionaryFromKeyValue(),
                    RequiredEntity = GameEvent.EventRequiredEntity.None,
                    GameTime = gameTime
                };
            }

            // this is a custom event printed out by _customcallbacks.gsc (used for team balance)
            if (eventType == "JoinTeam")
            {
                return new GameEvent()
                {
                    Type = GameEvent.EventType.JoinTeam,
                    Data = logLine,
                    Origin = new EFClient() { NetworkId = lineSplit[1].ConvertGuidToLong(Configuration.GuidNumberStyle) },
                    RequiredEntity = GameEvent.EventRequiredEntity.Target,
                    GameTime = gameTime
                };
            }

            // this is a custom event printed out by _customcallbacks.gsc (used for anticheat)
            if (eventType == "ScriptKill")
            {
                long originId = lineSplit[1].ConvertGuidToLong(Configuration.GuidNumberStyle, 1);
                long targetId = lineSplit[2].ConvertGuidToLong(Configuration.GuidNumberStyle, 1);

                return new GameEvent()
                {
                    Type = GameEvent.EventType.ScriptKill,
                    Data = logLine,
                    Origin = new EFClient() { NetworkId = originId },
                    Target = new EFClient() { NetworkId = targetId },
                    RequiredEntity = GameEvent.EventRequiredEntity.Origin | GameEvent.EventRequiredEntity.Target,
                    GameTime = gameTime
                };
            }

            // this is a custom event printed out by _customcallbacks.gsc (used for anticheat)
            if (eventType == "ScriptDamage")
            {
                long originId = lineSplit[1].ConvertGuidToLong(Configuration.GuidNumberStyle, 1);
                long targetId = lineSplit[2].ConvertGuidToLong(Configuration.GuidNumberStyle, 1);

                return new GameEvent()
                {
                    Type = GameEvent.EventType.ScriptDamage,
                    Data = logLine,
                    Origin = new EFClient() { NetworkId = originId },
                    Target = new EFClient() { NetworkId = targetId },
                    RequiredEntity = GameEvent.EventRequiredEntity.Origin | GameEvent.EventRequiredEntity.Target,
                    GameTime = gameTime
                };
            }

            return new GameEvent()
            {
                Type = GameEvent.EventType.Unknown,
                Data = logLine,
                Origin = Utilities.IW4MAdminClient(),
                Target = Utilities.IW4MAdminClient(),
                RequiredEntity = GameEvent.EventRequiredEntity.None,
                GameTime = gameTime
            };
        }
    }
}
