using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;
using IW4MAdmin.Plugins.Login.Commands;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.Login
{
    public class Plugin : IPlugin
    {
        public string Name => "Login";

        public float Version => Assembly.GetExecutingAssembly().GetName().Version.Major + Assembly.GetExecutingAssembly().GetName().Version.Minor / 10.0f;

        public string Author => "RaidMax";

        public static ConcurrentDictionary<int, bool> AuthorizedClients { get; private set; }
        private readonly IConfigurationHandler<Configuration> _configHandler;

        public Plugin(IConfigurationHandlerFactory configurationHandlerFactory)
        {
            _configHandler = configurationHandlerFactory.GetConfigurationHandler<Configuration>("LoginPluginSettings");
        }

        public Task OnEventAsync(GameEvent gameEvent, Server server)
        {
            if (gameEvent.IsRemote || _configHandler.Configuration().RequirePrivilegedClientLogin == false)
                return Task.CompletedTask;

            if (gameEvent.Type == GameEvent.EventType.Connect)
            {
                AuthorizedClients.TryAdd(gameEvent.Origin.ClientId, false);
                gameEvent.Origin.SetAdditionalProperty("IsLoggedIn", false);
            }

            if (gameEvent.Type == GameEvent.EventType.Disconnect)
            {
                AuthorizedClients.TryRemove(gameEvent.Origin.ClientId, out _);
            }
            
            return Task.CompletedTask;
        }

        public async Task OnLoadAsync(IManager manager)
        {
            AuthorizedClients = new ConcurrentDictionary<int, bool>();

            manager.CommandInterceptors.Add(gameEvent =>
            {
                if (gameEvent.Type != GameEvent.EventType.Command)
                {
                    return true;
                }

                if (gameEvent.Origin.Level < EFClient.Permission.Moderator ||
                    gameEvent.Origin.Level == EFClient.Permission.Console)
                    return true;

                if (gameEvent.Extra.GetType() == typeof(SetPasswordCommand) &&
                    gameEvent.Origin?.Password == null)
                    return true;

                if (gameEvent.Extra.GetType() == typeof(LoginCommand))
                    return true;

                if (gameEvent.Extra.GetType() == typeof(RequestTokenCommand))
                    return true;

                if (!AuthorizedClients[gameEvent.Origin.ClientId])
                {
                    return false;
                }

                gameEvent.Origin.SetAdditionalProperty("IsLoggedIn", true);
                return true;
            });

            await _configHandler.BuildAsync();
            if (_configHandler.Configuration() == null)
            {
                _configHandler.Set((Configuration)new Configuration().Generate());
                await _configHandler.Save();
            }
        }

        public Task OnTickAsync(Server S) => Task.CompletedTask;

        public Task OnUnloadAsync() => Task.CompletedTask;
    }
}
