using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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

        public async Task OnEventAsync(GameEvent E, Server S)
        {
            if (!Settings.Configuration().EnableProfanityDeterment)
                return;

            if (E.Type == GameEvent.EventType.Connect)
            {
                if (!ProfanityCounts.TryAdd(E.Origin.ClientId, new Tracking(E.Origin)))
                {
                    S.Logger.WriteWarning("Could not add client to profanity tracking");
                }

                var objectionalWords = Settings.Configuration().OffensiveWords;
                bool containsObjectionalWord = objectionalWords.FirstOrDefault(w => E.Origin.Name.ToLower().Contains(w)) != null;

                // we want to run regex against it just incase
                if (!containsObjectionalWord)
                {
                    foreach (string word in objectionalWords)
                    {
                        containsObjectionalWord |= Regex.IsMatch(E.Origin.Name.ToLower(), word);
                    }
                }

                if (containsObjectionalWord)
                {
                    await E.Origin.Kick(Settings.Configuration().ProfanityKickMessage, new Player()
                    {
                        ClientId = 1
                    });
                };
            }

            if (E.Type == GameEvent.EventType.Disconnect)
            {
                if (!ProfanityCounts.TryRemove(E.Origin.ClientId, out Tracking old))
                {
                    S.Logger.WriteWarning("Could not remove client from profanity tracking");
                }
            }

            if (E.Type == GameEvent.EventType.Say)
            {
                var objectionalWords = Settings.Configuration().OffensiveWords;
                bool containsObjectionalWord = false;

                foreach (string word in objectionalWords)
                {
                    containsObjectionalWord |= Regex.IsMatch(E.Origin.Name.ToLower(), word);

                    // break out early because there's at least one objectional word
                    if (containsObjectionalWord)
                    {
                        break;
                    }
                }

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

        public Task OnTickAsync(Server S) => Task.CompletedTask;

        public Task OnUnloadAsync() => Task.CompletedTask;
    }
}
