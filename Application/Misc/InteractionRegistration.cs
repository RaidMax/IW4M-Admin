using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Data.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Interfaces;
using InteractionRegistrationCallback =
    System.Func<int?, Data.Models.Reference.Game?, System.Threading.CancellationToken,
        System.Threading.Tasks.Task<SharedLibraryCore.Interfaces.IInteractionData>>;

namespace IW4MAdmin.Application.Misc;

public class InteractionRegistration : IInteractionRegistration
{
    private readonly ILogger<InteractionRegistration> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, InteractionRegistrationCallback> _interactions = new();

    public InteractionRegistration(ILogger<InteractionRegistration> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public void RegisterScriptInteraction(string interactionName, string source, Delegate interactionRegistration)
    {
        var plugin = _serviceProvider.GetRequiredService<IEnumerable<IPlugin>>()
            .FirstOrDefault(plugin => plugin.Name == source);

        if (plugin is not ScriptPlugin scriptPlugin)
        {
            return;
        }

        var wrappedDelegate = (int? clientId, Reference.Game? game, CancellationToken token) =>
            Task.FromResult(
                scriptPlugin.WrapDelegate<IInteractionData>(interactionRegistration, clientId, game, token));

        if (!_interactions.ContainsKey(interactionName))
        {
            _interactions.TryAdd(interactionName, wrappedDelegate);
        }
        else
        {
            _interactions[interactionName] = wrappedDelegate;
        }
    }

    public void RegisterInteraction(string interactionName, InteractionRegistrationCallback interactionRegistration)
    {
        if (!_interactions.ContainsKey(interactionName))
        {
            _interactions.TryAdd(interactionName, interactionRegistration);
        }
        else
        {
            _interactions[interactionName] = interactionRegistration;
        }
    }

    public void UnregisterInteraction(string interactionName)
    {
        if (_interactions.ContainsKey(interactionName))
        {
            _interactions.TryRemove(interactionName, out _);
        }
    }

    public async Task<IEnumerable<IInteractionData>> GetInteractions(int? clientId = null,
        Reference.Game? game = null, CancellationToken token = default)
    {
        return (await Task.WhenAll(_interactions.Select(async kvp =>
        {
            try
            {
                return await kvp.Value(clientId, game, token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Could not get interaction for interaction {InteractionName} and ClientId {ClientId}", kvp.Key,
                    clientId);
                return null;
            }
        }))).Where(interaction => interaction is not null);
    }

    public async Task<string> ProcessInteraction(string interactionId, int? clientId = null,
        Reference.Game? game = null, CancellationToken token = default)
    {
        if (!_interactions.ContainsKey(interactionId))
        {
            throw new ArgumentException($"Interaction with ID {interactionId} has not been registered");
        }

        try
        {
            var interaction = await _interactions[interactionId](clientId, game, token);

            if (interaction.Action is not null)
            {
                return await interaction.Action(clientId, game, token);
            }

            if (interaction.ScriptAction is not null)
            {
                foreach (var plugin in _serviceProvider.GetRequiredService<IEnumerable<IPlugin>>())
                {
                    if (plugin is not ScriptPlugin scriptPlugin || scriptPlugin.Name != interaction.Source)
                    {
                        continue;
                    }

                    return scriptPlugin.ExecuteAction<string>(interaction.ScriptAction, clientId, game, token);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not process interaction for interaction {InteractionName} and ClientId {ClientId}",
                interactionId,
                clientId);
        }

        return null;
    }
}
