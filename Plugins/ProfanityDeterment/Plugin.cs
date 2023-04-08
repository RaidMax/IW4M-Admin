using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Data.Models;
using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

namespace IW4MAdmin.Plugins.ProfanityDeterment;

public class Plugin : IPluginV2
{
    public string Name => "ProfanityDeterment";
    public string Version => Utilities.GetVersionAsString();
    public string Author => "RaidMax";

    private const string ProfanityKey = "_profanityInfringements";

    private readonly ProfanityDetermentConfiguration _configuration;

    public static void RegisterDependencies(IServiceCollection serviceProvider)
    {
        serviceProvider.AddConfiguration<ProfanityDetermentConfiguration>("ProfanityDetermentSettings");
    }

    public Plugin(ProfanityDetermentConfiguration configuration)
    {
        _configuration = configuration;

        if (!(_configuration?.EnableProfanityDeterment ?? false))
        {
            return;
        }

        IManagementEventSubscriptions.ClientStateInitialized += OnClientStateInitialized;
        IGameEventSubscriptions.ClientMessaged += GameEventSubscriptionsOnClientMessaged;
        IManagementEventSubscriptions.ClientStateDisposed += (clientEvent, _) =>
        {
            clientEvent.Client.SetAdditionalProperty(ProfanityKey, null);
            return Task.CompletedTask;
        };
    }

    private Task GameEventSubscriptionsOnClientMessaged(ClientMessageEvent clientEvent, CancellationToken token)
    {
        if (!_configuration?.EnableProfanityDeterment ?? false)
        {
            return Task.CompletedTask;
        }

        var offensiveWords = _configuration!.OffensiveWords;
        var containsOffensiveWord = false;
        var matchedFilters = new List<string>();

        foreach (var word in offensiveWords.Where(word =>
                     Regex.IsMatch(clientEvent.Message?.StripColors() ?? string.Empty, word,
                         RegexOptions.IgnoreCase)))
        {
            containsOffensiveWord = true;
            matchedFilters.Add(word);
        }

        if (!containsOffensiveWord)
        {
            return Task.CompletedTask;
        }

        var profanityInfringements = clientEvent.Origin.GetAdditionalProperty<int>(ProfanityKey);

        var sender = clientEvent.Server.AsConsoleClient();
        sender.AdministeredPenalties = new List<EFPenalty>
        {
            new()
            {
                AutomatedOffense = $"{clientEvent.Message} - {string.Join(",", matchedFilters)}"
            }
        };

        if (profanityInfringements >= _configuration.KickAfterInfringementCount)
        {
            clientEvent.Client.Kick(_configuration.ProfanityKickMessage, sender);
        }

        else if (profanityInfringements < _configuration.KickAfterInfringementCount)
        {
            clientEvent.Client.SetAdditionalProperty(ProfanityKey, profanityInfringements + 1);
            clientEvent.Client.Warn(_configuration.ProfanityWarningMessage, sender);
        }

        return Task.CompletedTask;
    }

    private Task OnClientStateInitialized(ClientStateInitializeEvent clientEvent, CancellationToken token)
    {
        if (!(_configuration?.EnableProfanityDeterment ?? false))
        {
            return Task.CompletedTask;
        }

        if (!_configuration.KickOnInfringingName)
        {
            return Task.CompletedTask;
        }

        clientEvent.Client.SetAdditionalProperty(ProfanityKey, 0);

        var offensiveWords = _configuration!.OffensiveWords;
        var matchedFilters = new List<string>();
        var containsOffensiveWord = false;

        foreach (var word in offensiveWords.Where(word =>
                     Regex.IsMatch(clientEvent.Client.CleanedName, word, RegexOptions.IgnoreCase)))
        {
            containsOffensiveWord = true;
            matchedFilters.Add(word);
            break;
        }

        if (!containsOffensiveWord)
        {
            return Task.CompletedTask;
        }

        var sender = Utilities.IW4MAdminClient(clientEvent.Client.CurrentServer);
        sender.AdministeredPenalties = new List<EFPenalty>
        {
            new()
            {
                AutomatedOffense = $"{clientEvent.Client.Name} - {string.Join(",", matchedFilters)}"
            }
        };

        clientEvent.Client.Kick(_configuration.ProfanityKickMessage, sender);

        return Task.CompletedTask;
    }
}
