using IW4MAdmin.Application.API.Master;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Refit;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Misc
{
    /// <summary>
    /// implementation of IMasterCommunication
    /// talks to the master server
    /// </summary>
    internal class MasterCommunication(
        ILogger<MasterCommunication> logger,
        ApplicationConfiguration appConfig,
        ITranslationLookup translationLookup,
        IMasterApi apiInstance,
        IManager manager)
        : IMasterCommunication
    {
        private readonly ILogger _logger = logger;
        private readonly BuildNumber _fallbackVersion = BuildNumber.Parse("99.99.99.99");
        private const int ApiVersion = 1;
        private bool _firstHeartBeat = true;
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
        private string _authorizationToken;

        /// <summary>
        /// checks for latest version of the application
        /// notifies user if an update is available
        /// </summary>
        /// <returns></returns>
        public async Task CheckVersion()
        {
            var version = new VersionInfo
            {
                CurrentVersionStable = _fallbackVersion
            };

            try
            {
                version = await apiInstance.GetVersion(ApiVersion);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Unable to retrieve IW4MAdmin version information");
            }

            if (Equals(version.CurrentVersionStable, _fallbackVersion))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(translationLookup["MANAGER_VERSION_FAIL"]);
                Console.ForegroundColor = ConsoleColor.Gray;
            }

#if !PRERELEASE
            else if (version.CurrentVersionStable > Program.Version)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"IW4MAdmin {translationLookup["MANAGER_VERSION_UPDATE"]} [v{version.CurrentVersionStable}]");
                Console.WriteLine(translationLookup["MANAGER_VERSION_CURRENT"].FormatExt($"[v{Program.Version}]"));
                Console.ForegroundColor = ConsoleColor.Gray;
            }
#else
            else if (version.CurrentVersionPrerelease > Program.Version)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"IW4MAdmin-Prerelease {_transLookup["MANAGER_VERSION_UPDATE"]} [v{version.CurrentVersionPrerelease.ToString()}-pr]");
                Console.WriteLine(_transLookup["MANAGER_VERSION_CURRENT"].FormatExt($"[v{Program.Version.ToString()}-pr]"));
                Console.ForegroundColor = ConsoleColor.Gray;
            }
#endif
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(translationLookup["MANAGER_VERSION_SUCCESS"]);
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }

        public async Task RunUploadStatus(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (manager.IsRunning)
                    {
                        await UploadStatus();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Could not send heartbeat - {Message}", ex.Message);
                }

                try
                {
                    await Task.Delay(Interval, token);
                }
                catch
                {
                    break;
                }
            }
        }

        private async Task UploadStatus()
        {
            if (_firstHeartBeat)
            {
                var token = await apiInstance.Authenticate(new AuthenticationId
                {
                    Id = appConfig.Id
                });

                _authorizationToken = $"Bearer {token.AccessToken}";
            }

            var instance = new ApiInstance
            {
                Id = appConfig.Id,
                Uptime = (int)(DateTime.UtcNow - ((ApplicationManager)manager).StartTime).TotalSeconds,
                Version = Program.Version,
                Servers = manager.GetServers().Select(s =>
                    new ApiServer
                    {
                        ClientNum = s.ClientNum,
                        Game = s.GameName.ToString(),
                        Version = s.Version,
                        Gametype = s.Gametype,
                        Hostname = s.Hostname,
                        Map = s.CurrentMap.Name,
                        MaxClientNum = s.MaxClients,
                        Id = s.EndPoint,
                        Port = (short)s.ListenPort,
                        IPAddress = s.ListenAddress
                    }).ToList(),
                WebfrontUrl = appConfig.WebfrontUrl
            };

            IApiResponse<ResultMessage> response;

            if (_firstHeartBeat)
            {
                response = await apiInstance.AddInstance(instance, _authorizationToken);
            }
            else
            {
                response = await apiInstance.UpdateInstance(instance.Id, instance, _authorizationToken);
                _firstHeartBeat = false;
            }

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                _logger.LogWarning("Non success response code from master is {StatusCode}, message is {Message}", response.StatusCode,
                    response.Error?.Content);
            }
        }
    }
}
