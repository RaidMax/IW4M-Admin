using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Objects;

namespace IW4MAdmin.Plugins.ProfanityDeterment
{
    public class Plugin : IPlugin
    {
        public string Name => "ProfanityDeterment";

        public float Version => Assembly.GetExecutingAssembly().GetName().Version.Major + Assembly.GetExecutingAssembly().GetName().Version.Minor / 10.0f;

        public string Author => "RaidMax";

        BaseConfigurationHandler<Configuration> Settings;
        ConcurrentDictionary<int, Tracking> ProfanityCounts;
        IManager Manager;
        Task CompletedTask = Task.FromResult(false);

        public async Task OnEventAsync(Event E, Server S)
        {
            if (!Settings.Configuration().EnableProfanityDeterment)
                return;

            if (E.Type == Event.GType.Connect)
            {
                if (!ProfanityCounts.TryAdd(E.Origin.ClientId, new Tracking(E.Origin)))
                {
                    S.Logger.WriteWarning("Could not add client to profanity tracking");
                }

            }

            if (E.Type == Event.GType.Disconnect)
            {
                if (!ProfanityCounts.TryRemove(E.Origin.ClientId, out Tracking old))
                {
                    S.Logger.WriteWarning("Could not remove client from profanity tracking");
                }
            }

            if (E.Type == Event.GType.Say)
            {
                var objectionalWords = Settings.Configuration().OffensiveWords;
                bool containsObjectionalWord = objectionalWords.FirstOrDefault(w => E.Data.ToLower().Contains(w)) != null;

                if (containsObjectionalWord)
                {
                    var clientProfanity = ProfanityCounts[E.Origin.ClientId];
                    if (clientProfanity.Infringements >= Settings.Configuration().KickAfterInfringementCount)
                    {
                        await clientProfanity.Client.Kick(Settings.Configuration().ProfanityKickMessage, new Player()
                        {
                            ClientId = 1
                        });
                    }

                    else if (clientProfanity.Infringements < Settings.Configuration().KickAfterInfringementCount)
                    {
                        clientProfanity.Infringements++;

                        await clientProfanity.Client.Warn(Settings.Configuration().ProfanityWarningMessage, new Player()
                        {
                            ClientId = 1
                        });
                    }
                }
            }
        }

        public async Task OnLoadAsync(IManager manager)
        {
            // load custom configuration
            Settings = new BaseConfigurationHandler<Configuration>("ProfanityDetermentSettings");
            if (Settings.Configuration() == null)
            {
                Settings.Set((Configuration)new Configuration().Generate());
                await Settings.Save();
            }

            ProfanityCounts = new ConcurrentDictionary<int, Tracking>();
            Manager = manager;
        }

        public Task OnTickAsync(Server S) => CompletedTask;

        public Task OnUnloadAsync() => CompletedTask;
    }
}
