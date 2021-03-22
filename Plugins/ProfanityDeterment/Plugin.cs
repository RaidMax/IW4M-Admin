using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Data.Models;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.ProfanityDeterment
{
    public class Plugin : IPlugin
    {
        public string Name => "ProfanityDeterment";

        public float Version => Assembly.GetExecutingAssembly().GetName().Version.Major + Assembly.GetExecutingAssembly().GetName().Version.Minor / 10.0f;

        public string Author => "RaidMax";

        private readonly IConfigurationHandler<Configuration> _configHandler;

        public Plugin(IConfigurationHandlerFactory configurationHandlerFactory)
        {
            _configHandler = configurationHandlerFactory.GetConfigurationHandler<Configuration>("ProfanityDetermentSettings");
        }

        public Task OnEventAsync(GameEvent E, Server S)
        {
            if (!_configHandler.Configuration().EnableProfanityDeterment)
                return Task.CompletedTask;

            if (E.Type == GameEvent.EventType.Connect)
            {
                E.Origin.SetAdditionalProperty("_profanityInfringements", 0);

                var objectionalWords = _configHandler.Configuration().OffensiveWords;
                var matchedFilters = new List<string>();
                bool containsObjectionalWord = false;

                foreach (string word in objectionalWords)
                {
                    if (Regex.IsMatch(E.Origin.Name.ToLower(), word, RegexOptions.IgnoreCase))
                    {
                        containsObjectionalWord |= true;
                        matchedFilters.Add(word);
                    }
                }

                if (containsObjectionalWord)
                {
                    var sender = Utilities.IW4MAdminClient(E.Owner);
                    sender.AdministeredPenalties = new List<EFPenalty>()
                    {
                        new EFPenalty()
                        {
                            AutomatedOffense = $"{E.Origin.Name} - {string.Join(",", matchedFilters)}"
                        }
                    };
                    E.Origin.Kick(_configHandler.Configuration().ProfanityKickMessage, sender);
                };
            }

            if (E.Type == GameEvent.EventType.Disconnect)
            {
                E.Origin.SetAdditionalProperty("_profanityInfringements", 0);
            }

            if (E.Type == GameEvent.EventType.Say)
            {
                var objectionalWords = _configHandler.Configuration().OffensiveWords;
                bool containsObjectionalWord = false;
                var matchedFilters = new List<string>();

                foreach (string word in objectionalWords)
                {
                    if (Regex.IsMatch(E.Data.ToLower(), word, RegexOptions.IgnoreCase))
                    {
                        containsObjectionalWord |= true;
                        matchedFilters.Add(word);
                    }
                }

                if (containsObjectionalWord)
                {
                    int profanityInfringments = E.Origin.GetAdditionalProperty<int>("_profanityInfringements");

                    var sender = Utilities.IW4MAdminClient(E.Owner);
                    sender.AdministeredPenalties = new List<EFPenalty>()
                    {
                        new EFPenalty()
                        {
                            AutomatedOffense = $"{E.Data} - {string.Join(",", matchedFilters)}"
                        }
                    };

                    if (profanityInfringments >= _configHandler.Configuration().KickAfterInfringementCount)
                    {
                        E.Origin.Kick(_configHandler.Configuration().ProfanityKickMessage, sender);
                    }

                    else if (profanityInfringments < _configHandler.Configuration().KickAfterInfringementCount)
                    {
                        E.Origin.SetAdditionalProperty("_profanityInfringements", profanityInfringments + 1);
                        E.Origin.Warn(_configHandler.Configuration().ProfanityWarningMessage, sender);
                    }
                }
            }
            return Task.CompletedTask;
        }

        public async Task OnLoadAsync(IManager manager)
        {
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
