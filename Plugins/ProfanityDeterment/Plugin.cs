using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.ProfanityDeterment
{
    public class Plugin : IPlugin
    {
        public string Name => "ProfanityDeterment";

        public float Version => Assembly.GetExecutingAssembly().GetName().Version.Major + Assembly.GetExecutingAssembly().GetName().Version.Minor / 10.0f;

        public string Author => "RaidMax";

        BaseConfigurationHandler<Configuration> Settings;

        public Task OnEventAsync(GameEvent E, Server S)
        {
            if (!Settings.Configuration().EnableProfanityDeterment)
                return Task.CompletedTask;

            if (E.Type == GameEvent.EventType.Connect)
            {
                E.Origin.SetAdditionalProperty("_profanityInfringements", 0);

                var objectionalWords = Settings.Configuration().OffensiveWords;
                bool containsObjectionalWord = objectionalWords.FirstOrDefault(w => E.Origin.Name.ToLower().Contains(w)) != null;

                // we want to run regex against it just incase
                if (!containsObjectionalWord)
                {
                    foreach (string word in objectionalWords)
                    {
                        containsObjectionalWord |= Regex.IsMatch(E.Origin.Name.ToLower(), word, RegexOptions.IgnoreCase);
                    }
                }

                if (containsObjectionalWord)
                {
                    E.Origin.Kick(Settings.Configuration().ProfanityKickMessage, Utilities.IW4MAdminClient(E.Owner));
                };
            }

            if (E.Type == GameEvent.EventType.Disconnect)
            {
                E.Origin.SetAdditionalProperty("_profanityInfringements", 0);
            }

            if (E.Type == GameEvent.EventType.Say)
            {
                var objectionalWords = Settings.Configuration().OffensiveWords;
                bool containsObjectionalWord = false;

                foreach (string word in objectionalWords)
                {
                    containsObjectionalWord |= Regex.IsMatch(E.Data.ToLower(), word, RegexOptions.IgnoreCase);

                    // break out early because there's at least one objectional word
                    if (containsObjectionalWord)
                    {
                        break;
                    }
                }

                if (containsObjectionalWord)
                {
                    int profanityInfringments = E.Origin.GetAdditionalProperty<int>("_profanityInfringements");

                    if (profanityInfringments >= Settings.Configuration().KickAfterInfringementCount)
                    {
                        E.Origin.Kick(Settings.Configuration().ProfanityKickMessage, Utilities.IW4MAdminClient(E.Owner));
                    }

                    else if (profanityInfringments < Settings.Configuration().KickAfterInfringementCount)
                    {
                        E.Origin.SetAdditionalProperty("_profanityInfringements", profanityInfringments + 1);
                        E.Origin.Warn(Settings.Configuration().ProfanityWarningMessage, Utilities.IW4MAdminClient(E.Owner));
                    }
                }
            }
            return Task.CompletedTask;
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
        }

        public Task OnTickAsync(Server S) => Task.CompletedTask;

        public Task OnUnloadAsync() => Task.CompletedTask;
    }
}
