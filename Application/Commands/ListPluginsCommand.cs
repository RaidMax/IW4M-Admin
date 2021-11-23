using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Commands
{
    /// <summary>
    /// Lists the loaded plugins
    /// </summary>
    public class ListPluginsCommand : Command
    {
        private readonly IEnumerable<IPlugin> _plugins;

        public ListPluginsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
            IEnumerable<IPlugin> plugins) : base(config, translationLookup)
        {
            Name = "plugins";
            Description = _translationLookup["COMMANDS_PLUGINS_DESC"];
            Alias = "p";
            Permission = EFClient.Permission.Administrator;
            RequiresTarget = false;
            _plugins = plugins;
        }

        public override Task ExecuteAsync(GameEvent gameEvent)
        {
            gameEvent.Origin.Tell(_translationLookup["COMMANDS_PLUGINS_LOADED"]);
            foreach (var plugin in _plugins.Where(plugin => !plugin.IsParser))
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_LIST_PLUGINS_FORMAT"]
                    .FormatExt(plugin.Name, plugin.Version, plugin.Author));
            }

            return Task.CompletedTask;
        }
    }
}