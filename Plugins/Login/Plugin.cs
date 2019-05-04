using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
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
        private Configuration Config;

        public Task OnEventAsync(GameEvent E, Server S)
        {
            if (E.IsRemote || Config.RequirePrivilegedClientLogin == false)
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

                if (((Command)E.Extra).Name == new SharedLibraryCore.Commands.CSetPassword().Name &&
                    E.Origin?.Password == null)
                    return Task.CompletedTask;

                if (((Command)E.Extra).Name == new Commands.CLogin().Name)
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

            var cfg = new BaseConfigurationHandler<Configuration>("LoginPluginSettings");
            if (cfg.Configuration() == null)
            {
                cfg.Set((Configuration)new Configuration().Generate());
                await cfg.Save();
            }

            Config = cfg.Configuration();
        }

        public Task OnTickAsync(Server S) => Task.CompletedTask;

        public Task OnUnloadAsync() => Task.CompletedTask;
    }
}
