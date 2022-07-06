using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.RCon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Data.Models;
using IW4MAdmin.Application.Misc;
using Microsoft.Extensions.Logging;
using static SharedLibraryCore.Server;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.RConParsers
{
    public class BaseRConParser : IRConParser
    {
        private readonly ILogger _logger;
        private static string _botIpIndicator = "00000000.";
        
        public BaseRConParser(ILogger<BaseRConParser> logger, IParserRegexFactory parserRegexFactory)
        {
            _logger = logger;
            Configuration = new DynamicRConParserConfiguration(parserRegexFactory)
            {
                CommandPrefixes = new CommandPrefix()
                {
                    Tell = "tell {0} {1}",
                    Say = "say {0}",
                    Kick = "clientkick {0} \"{1}\"",
                    Ban = "clientkick {0} \"{1}\"",
                    TempBan = "tempbanclient {0} \"{1}\"",
                    RConCommand = "ÿÿÿÿrcon {0} {1}",
                    RConGetDvar = "ÿÿÿÿrcon {0} {1}",
                    RConSetDvar = "ÿÿÿÿrcon {0} set {1}",
                    RConGetStatus = "ÿÿÿÿgetstatus",
                    RConGetInfo = "ÿÿÿÿgetinfo",
                    RConResponse = "ÿÿÿÿprint",
                    RconGetInfoResponseHeader = "ÿÿÿÿinfoResponse"
                },
                ServerNotRunningResponse = "Server is not running."
            };

            Configuration.Status.Pattern = @"^ *([0-9]+) +-?([0-9]+) +((?:[A-Z]+|[0-9]+)) +((?:[a-z]|[0-9]){8,32}|(?:[a-z]|[0-9]){8,32}|bot[0-9]+|(?:[0-9]+)) *(.{0,32}) +([0-9]+) +(\d+\.\d+\.\d+.\d+\:-*\d{1,5}|0+.0+:-*\d{1,5}|loopback|unknown) +(-*[0-9]+) +([0-9]+) *$";
            Configuration.Status.AddMapping(ParserRegex.GroupType.RConClientNumber, 1);
            Configuration.Status.AddMapping(ParserRegex.GroupType.RConScore, 2);
            Configuration.Status.AddMapping(ParserRegex.GroupType.RConPing, 3);
            Configuration.Status.AddMapping(ParserRegex.GroupType.RConNetworkId, 4);
            Configuration.Status.AddMapping(ParserRegex.GroupType.RConName, 5);
            Configuration.Status.AddMapping(ParserRegex.GroupType.RConIpAddress, 7);

            Configuration.Dvar.Pattern = "^\"(.+)\" is: \"(.+)?\" default: \"(.+)?\"\n?(?:latched: \"(.+)?\"\n?)? *(.+)$";
            Configuration.Dvar.AddMapping(ParserRegex.GroupType.RConDvarName, 1);
            Configuration.Dvar.AddMapping(ParserRegex.GroupType.RConDvarValue, 2);
            Configuration.Dvar.AddMapping(ParserRegex.GroupType.RConDvarDefaultValue, 3);
            Configuration.Dvar.AddMapping(ParserRegex.GroupType.RConDvarLatchedValue, 4);
            Configuration.Dvar.AddMapping(ParserRegex.GroupType.RConDvarDomain, 5);
            Configuration.Dvar.AddMapping(ParserRegex.GroupType.AdditionalGroup, int.MaxValue);

            Configuration.StatusHeader.Pattern = "num +score +ping +guid +name +lastmsg +address +qport +rate *";
            Configuration.GametypeStatus.Pattern = "";
            Configuration.MapStatus.Pattern = @"map: (([a-z]|_|\d)+)";
            Configuration.MapStatus.AddMapping(ParserRegex.GroupType.RConStatusMap, 1);

            if (!Configuration.DefaultDvarValues.ContainsKey("mapname"))
            {
                Configuration.DefaultDvarValues.Add("mapname", "Unknown");
            }
        }

        public IRConParserConfiguration Configuration { get; set; }
        public virtual string Version { get; set; } = "CoD";
        public Game GameName { get; set; } = Game.COD;
        public bool CanGenerateLogPath { get; set; } = true;
        public string Name { get; set; } = "Call of Duty";
        public string RConEngine { get; set; } = "COD";
        public bool IsOneLog { get; set; }

        public async Task<string[]> ExecuteCommandAsync(IRConConnection connection, string command, CancellationToken token = default)
        {
            command = command.FormatMessageForEngine(Configuration);
            var response = await connection.SendQueryAsync(StaticHelpers.QueryType.COMMAND, command, token);
            return response.Where(item => item != Configuration.CommandPrefixes.RConResponse).ToArray();
        }

        public async Task<Dvar<T>> GetDvarAsync<T>(IRConConnection connection, string dvarName, T fallbackValue = default, CancellationToken token = default)
        {
            string[] lineSplit;

            try
            {
                lineSplit = await connection.SendQueryAsync(StaticHelpers.QueryType.GET_DVAR, dvarName, token);
            }
            catch
            {
                if (fallbackValue == null)
                {
                    throw;
                }

                lineSplit = Array.Empty<string>();
            }

            var response = string.Join('\n', lineSplit).Replace("\n", "").TrimEnd('\0');
            var match = Regex.Match(response, Configuration.Dvar.Pattern);

            if (response.Contains("Unknown command") ||
                !match.Success)
            {
                if (fallbackValue != null)
                {
                    return new Dvar<T>
                    {
                        Name = dvarName,
                        Value = fallbackValue
                    };
                }

                throw new DvarException(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_DVAR"].FormatExt(dvarName));
            }

            var value = match.Groups[Configuration.Dvar.GroupMapping[ParserRegex.GroupType.RConDvarValue]].Value;
            var defaultValue = match.Groups[Configuration.Dvar.GroupMapping[ParserRegex.GroupType.RConDvarDefaultValue]].Value;
            var latchedValue = match.Groups[Configuration.Dvar.GroupMapping[ParserRegex.GroupType.RConDvarLatchedValue]].Value;

            string RemoveTrailingColorCode(string input) => Regex.Replace(input, @"\^7$", "");

            value = RemoveTrailingColorCode(value);
            defaultValue = RemoveTrailingColorCode(defaultValue);
            latchedValue = RemoveTrailingColorCode(latchedValue);

            return new Dvar<T>
            {
                Name = dvarName,
                Value = string.IsNullOrEmpty(value) ? default : (T)Convert.ChangeType(value, typeof(T)),
                DefaultValue = string.IsNullOrEmpty(defaultValue) ? default : (T)Convert.ChangeType(defaultValue, typeof(T)),
                LatchedValue = string.IsNullOrEmpty(latchedValue) ? default : (T)Convert.ChangeType(latchedValue, typeof(T)),
                Domain = match.Groups[Configuration.Dvar.GroupMapping[ParserRegex.GroupType.RConDvarDomain]].Value
            };
        }

        public void BeginGetDvar(IRConConnection connection, string dvarName, AsyncCallback callback, CancellationToken token = default)
        {
            GetDvarAsync<string>(connection, dvarName, token: token).ContinueWith(action =>
            {
                if (action.Exception is null)
                {
                    callback?.Invoke(new AsyncResult
                    {
                        IsCompleted = true,
                        AsyncState = (true, action.Result.Value)
                    });
                }

                else
                {
                    callback?.Invoke(new AsyncResult
                    {
                        IsCompleted = true,
                        AsyncState = (false, (string)null)
                    });
                }
            }, token);
        }

        public virtual async Task<IStatusResponse> GetStatusAsync(IRConConnection connection, CancellationToken token = default)
        {
            var response = await connection.SendQueryAsync(StaticHelpers.QueryType.COMMAND_STATUS, "status", token);
            
            _logger.LogDebug("Status Response {Response}", string.Join(Environment.NewLine, response));
            
            return new StatusResponse
            {
                Clients = ClientsFromStatus(response).ToArray(),
                Map = GetValueFromStatus<string>(response, ParserRegex.GroupType.RConStatusMap, Configuration.MapStatus.Pattern),
                GameType = GetValueFromStatus<string>(response, ParserRegex.GroupType.RConStatusGametype, Configuration.GametypeStatus.Pattern),
                Hostname = GetValueFromStatus<string>(response, ParserRegex.GroupType.RConStatusHostname, Configuration.HostnameStatus.Pattern),
                MaxClients = GetValueFromStatus<int?>(response, ParserRegex.GroupType.RConStatusMaxPlayers, Configuration.MaxPlayersStatus.Pattern)
            };
        }

        private T GetValueFromStatus<T>(IEnumerable<string> response, ParserRegex.GroupType groupType, string groupPattern)
        {
            if (string.IsNullOrEmpty(groupPattern))
            {
                return default;
            }
            
            string value = null;
            foreach (var line in response)
            {
                var regex = Regex.Match(line, groupPattern);
                if (regex.Success)
                {
                    value = regex.Groups[Configuration.MapStatus.GroupMapping[groupType]].ToString();
                }
            }

            if (value == null)
            {
                return default;
            }

            if (typeof(T) == typeof(int?))
            {
                return (T)Convert.ChangeType(int.Parse(value), Nullable.GetUnderlyingType(typeof(T)));
            }
            
            return (T)Convert.ChangeType(value, typeof(T));
        }

        public async Task<bool> SetDvarAsync(IRConConnection connection, string dvarName, object dvarValue, CancellationToken token = default)
        {
            var dvarString = (dvarValue is string str)
                ? $"{dvarName} \"{str}\""
                : $"{dvarName} {dvarValue}";

            return (await connection.SendQueryAsync(StaticHelpers.QueryType.SET_DVAR, dvarString, token)).Length > 0;
        }

        public void BeginSetDvar(IRConConnection connection, string dvarName, object dvarValue, AsyncCallback callback,
            CancellationToken token = default)
        {
            SetDvarAsync(connection, dvarName, dvarValue, token).ContinueWith(action =>
            {
                if (action.Exception is null)
                {
                    callback?.Invoke(new AsyncResult
                    {
                        IsCompleted = true,
                        AsyncState = true
                    });
                }

                else
                {
                    callback?.Invoke(new AsyncResult
                    {
                        IsCompleted = true,
                        AsyncState = false
                    });
                }
            }, token);
        }

        private List<EFClient> ClientsFromStatus(string[] Status)
        {
            List<EFClient> StatusPlayers = new List<EFClient>();

            bool parsedHeader = false;
            foreach (string statusLine in Status)
            {
                string responseLine = statusLine.Trim();

                if (Configuration.StatusHeader.PatternMatcher.Match(responseLine).Success)
                {
                    parsedHeader = true;
                    continue;
                }

                var match = Configuration.Status.PatternMatcher.Match(responseLine);

                if (match.Success)
                {
                    if (match.Values[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConPing]] == "ZMBI")
                    {
                        _logger.LogDebug("Ignoring detected client {client} because they are zombie state", string.Join(",", match.Values));
                        continue;
                    }
                    
                    var clientNumber = int.Parse(match.Values[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConClientNumber]]);
                    var score = 0;
                    
                    if (Configuration.Status.GroupMapping[ParserRegex.GroupType.RConScore] > 0)
                    {
                        score = int.Parse(match.Values[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConScore]]);
                    }

                    var ping = 999;

                    // their state can be CNCT, ZMBI etc
                    if (match.Values[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConPing]].Length <= 3)
                    {
                        ping = int.Parse(match.Values[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConPing]]);
                    }

                    long networkId;
                    var name = match.Values[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConName]].TrimNewLine();
                    string networkIdString;
                    
                    var ip = match.Values[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConIpAddress]].Split(':')[0].ConvertToIP();

                    if (match.Values[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConIpAddress]]
                        .Contains(_botIpIndicator))
                    {
                        ip = System.Net.IPAddress.Broadcast.ToString().ConvertToIP();
                    }

                    try
                    {
                        networkIdString = match.Values[Configuration.Status.GroupMapping[ParserRegex.GroupType.RConNetworkId]];

                        networkId = networkIdString.IsBotGuid() || (ip == null && ping == 999) ?
                            name.GenerateGuidFromString() :
                            networkIdString.ConvertGuidToLong(Configuration.GuidNumberStyle);
                    }

                    catch (FormatException)
                    {
                        continue;
                    }

                    var client = new EFClient
                    {
                        CurrentAlias = new EFAlias
                        {
                            Name = name,
                            IPAddress = ip
                        },
                        NetworkId = networkId,
                        ClientNumber = clientNumber,
                        Ping = ping,
                        Score = score,
                        State = EFClient.ClientState.Connecting
                    };

                    client.SetAdditionalProperty("BotGuid", networkIdString);

                    if (Configuration.Status.GroupMapping.ContainsKey(ParserRegex.GroupType.AdditionalGroup))
                    {
                        var additionalGroupIndex =
                            Configuration.Status.GroupMapping[ParserRegex.GroupType.AdditionalGroup];
                        
                        if (match.Values.Length > additionalGroupIndex)
                        {
                            client.SetAdditionalProperty("ConnectionClientId", match.Values[additionalGroupIndex]);
                        }
                    }

                    StatusPlayers.Add(client);
                }
            }

            // this can happen if status is requested while map is rotating and we get a log dump back
            if (!parsedHeader)
            {
                throw new ServerException(Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_UNEXPECTED_STATUS"]);
            }

            return StatusPlayers;
        }

        public string GetOverrideDvarName(string dvarName)
        {
            if (Configuration.OverrideDvarNameMapping.ContainsKey(dvarName))
            {
                return Configuration.OverrideDvarNameMapping[dvarName];
            }

            return dvarName;
        }

        public T GetDefaultDvarValue<T>(string dvarName) => Configuration.DefaultDvarValues.ContainsKey(dvarName) ?
            (T)Convert.ChangeType(Configuration.DefaultDvarValues[dvarName], typeof(T)) :
            default;

        public TimeSpan? OverrideTimeoutForCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
            {
                return TimeSpan.Zero;
            }
            
            var commandToken = command.Split(' ', StringSplitOptions.RemoveEmptyEntries).First().ToLower();

            if (!Configuration.OverrideCommandTimeouts.ContainsKey(commandToken))
            {
                return TimeSpan.Zero;
            }

            var timeoutValue = Configuration.OverrideCommandTimeouts[commandToken];
            
            if (timeoutValue.HasValue && timeoutValue.Value != 0) // JINT doesn't seem to be able to properly set nulls on dictionaries
            {
                return TimeSpan.FromSeconds(timeoutValue.Value);
            }

            return null;
        }
    }
}
