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

        public Task OnEventAsync(GameEvent E, Server S)
        {
            if (E.IsRemote || _configHandler.Configuration().RequirePrivilegedClientLogin == false)
                return Task.CompletedTask;

            if (E.Type == GameEvent.EventType.Connect)
            {
                AuthorizedClients.TryAdd(E.Origin.ClientId, false);
                E.Origin.SetAdditionalProperty("IsLoggedIn", false);
            }

            if (E.Type == GameEvent.EventType.Disconnect)
            {
                AuthorizedClients.TryRemove(E.Origin.ClientId, out bool value);
            }

            if (E.Type == GameEvent.EventType.Command)
            {
                if (E.Origin.Level < EFClient.Permission.Moderator ||
                    E.Origin.Level == EFClient.Permission.Console)
                    return Task.CompletedTask;

                if (E.Extra.GetType() == typeof(SetPasswordCommand) &&
                    E.Origin?.Password == null)
                    return Task.CompletedTask;

                if (E.Extra.GetType() == typeof(LoginCommand))
                    return Task.CompletedTask;

                if (E.Extra.GetType() == typeof(RequestTokenCommand))
                    return Task.CompletedTask;

                if (!AuthorizedClients[E.Origin.ClientId])
                {
                    throw new AuthorizationException(Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_LOGIN_AUTH"]);
                }

                else
                {
                    E.Origin.SetAdditionalProperty("IsLoggedIn", true);
                }
            }

            return Task.CompletedTask;
        }

        public async Task OnLoadAsync(IManager manager)
        {
            AuthorizedClients = new ConcurrentDictionary<int, bool>();

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
