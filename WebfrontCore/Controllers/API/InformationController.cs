using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Components.Features.Home.Models;
using WebfrontCore.Components.Features.Console.Models;

namespace WebfrontCore.Controllers.API
{
    [ApiController]
    [Route("api/[controller]")]
    public class InformationController : BaseController
    {
        private readonly ApplicationConfiguration _appConfig;
        private readonly ITranslationLookup _translationLookup;
        private readonly ILookup<System.Type, string> _pluginTypeNames;

        public InformationController(IManager manager, ApplicationConfiguration appConfig, ITranslationLookup translationLookup, IEnumerable<IPlugin> v1Plugins, IEnumerable<IPluginV2> v2Plugins) : base(manager)
        {
            _appConfig = appConfig;
            _translationLookup = translationLookup;
             _pluginTypeNames = v1Plugins.Select(plugin => (plugin.GetType(), plugin.Name))
                .Concat(v2Plugins.Select(plugin => (plugin.GetType(), plugin.Name)))
                .ToLookup(selector => selector.Item1, selector => selector.Name);
        }

        [HttpGet("about")]
        public ActionResult<AboutInfo> GetAbout()
        {
             var activeServers = _appConfig.Servers.Where(server =>
                Manager.GetServers().FirstOrDefault(s => s.ListenAddress == server.IPAddress && s.ListenPort == server.Port) != null);

            var serverRules = activeServers.Select(config =>
            {
                var server = Manager.GetServers().First(server =>
                    server.ListenAddress == config.IPAddress && server.ListenPort == config.Port);
                return new ServerRulesInfo
                {
                    ServerName = server.ServerName,
                    IPAddress = server.ListenAddress,
                    Port = server.ListenPort,
                    Rules = config.Rules
                };
            }).ToList();

            return new AboutInfo
            {
                CommunityInformation = _appConfig.CommunityInformation,
                GlobalRules = _appConfig.GlobalRules,
                ServerRules = serverRules
            };
        }

        [HttpGet("help")]
        public ActionResult<List<CommandGroupInfo>> GetHelp()
        {
             var userLevel = Authorized ? Data.Models.Client.EFClient.Permission.Owner : Data.Models.Client.EFClient.Permission.User;
             if (User.Identity.IsAuthenticated)
            {
                 var levelClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value;
                 if (System.Enum.TryParse(levelClaim, out Data.Models.Client.EFClient.Permission result))
                 {
                     userLevel = result;
                 }
            }

            var commands = Manager.GetCommands()
                .Where(command => command.Permission <= userLevel)
                .OrderByDescending(command => command.Permission)
                .GroupBy(command =>
                {
                    if (command.GetType().Name == "ScriptCommand")
                    {
                        return _translationLookup["WEBFRONT_HELP_SCRIPT_PLUGIN"];
                    }

                    var assemblyName = command.GetType().Assembly.GetName().Name;
                    if (assemblyName is "IW4MAdmin" or "SharedLibraryCore")
                    {
                        return _translationLookup["WEBFRONT_HELP_COMMAND_NATIVE"];
                    }

                    var pluginType = command.GetType().Assembly.GetTypes()
                        .FirstOrDefault(type => typeof(IPlugin).IsAssignableFrom(type) || typeof(IPluginV2).IsAssignableFrom(type));

                    if (pluginType == null)
                    {
                         return _translationLookup["WEBFRONT_HELP_COMMAND_NATIVE"];
                    }

                    return _pluginTypeNames[pluginType].FirstOrDefault() ?? _translationLookup["WEBFRONT_HELP_COMMAND_NATIVE"];
                })
                .Select(group => new CommandGroupInfo
                {
                    Name = group.Key,
                    Commands = group.Select(c => new CommandInfo
                    {
                        Name = c.Name,
                        Alias = c.Alias,
                        Description = c.Description,
                        Syntax = c.Syntax,
                        RequiresTarget = c.RequiresTarget,
                        Permission = c.Permission,
                        SupportedGames = c.SupportedGames
                    }).ToList()
                }).ToList();

            return commands;
        }
    }
}


