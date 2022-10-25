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
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Script interaction source cannot be null");
        }
        
        _logger.LogDebug("Registering script interaction {InteractionName} from {Source}", interactionName, source);
        
        var plugin = _serviceProvider.GetRequiredService<IEnumerable<IPlugin>>()
            .FirstOrDefault(plugin => plugin.Name == source);

        if (plugin is not ScriptPlugin scriptPlugin)
        {
            return;
        }

        Task<IInteractionData> WrappedDelegate(int? clientId, Reference.Game? game, CancellationToken token) =>
            Task.FromResult(
                scriptPlugin.WrapDelegate<IInteractionData>(interactionRegistration, token, clientId, game, token));

        if (!_interactions.ContainsKey(interactionName))
        {
            _interactions.TryAdd(interactionName, WrappedDelegate);
        }
        else
        {
            _interactions[interactionName] = WrappedDelegate;
        }
    }

    public void RegisterInteraction(string interactionName, InteractionRegistrationCallback interactionRegistration)
    {
        if (!_interactions.ContainsKey(interactionName))
        {
            _logger.LogDebug("Registering interaction {InteractionName}", interactionName);
            _interactions.TryAdd(interactionName, interactionRegistration);
        }
        else
        {
            _logger.LogDebug("Updating interaction {InteractionName}", interactionName);
            _interactions[interactionName] = interactionRegistration;
        }
    }

    public void UnregisterInteraction(string interactionName)
    {
        if (!_interactions.ContainsKey(interactionName))
        {
            return;
        }

        _logger.LogDebug("Unregistering interaction {InteractionName}", interactionName);
        _interactions.TryRemove(interactionName, out _);
    }

    public async Task<IEnumerable<IInteractionData>> GetInteractions(string interactionPrefix = null,
        int? clientId = null,
        Reference.Game? game = null, CancellationToken token = default)
    {
        return await GetInteractionsInternal(interactionPrefix, clientId, game, token);
    }

    public async Task<string> ProcessInteraction(string interactionId, int originId, int? targetId = null,
        Reference.Game? game = null, IDictionary<string, string> meta = null, CancellationToken token = default)
    {
        if (!_interactions.ContainsKey(interactionId))
        {
            throw new ArgumentException($"Interaction with ID {interactionId} has not been registered");
        }

        try
        {
            var interaction = await _interactions[interactionId](targetId, game, token);

            if (interaction.Action is not null)
            {
                return await interaction.Action(originId, targetId, game, meta, token);
            }

            if (interaction.ScriptAction is not null)
            {
                foreach (var plugin in _serviceProvider.GetRequiredService<IEnumerable<IPlugin>>())
                {
                    if (plugin is not ScriptPlugin scriptPlugin || scriptPlugin.Name != interaction.Source)
                    {
                        continue;
                    }

                    return scriptPlugin.ExecuteAction<string>(interaction.ScriptAction, token, originId, targetId, game, meta,
                        token);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not process interaction for {InteractionName} and OriginId {ClientId}",
                interactionId, originId);
        }

        return null;
    }

    private async Task<IEnumerable<IInteractionData>> GetInteractionsInternal(string prefix = null,
        int? clientId = null, Reference.Game? game = null, CancellationToken token = default)
    {
        var interactions = _interactions
            .Where(interaction => string.IsNullOrWhiteSpace(prefix) || interaction.Key.StartsWith(prefix)).Select(
                async kvp =>
                {
                    try
                    {
                        return await kvp.Value(clientId, game, token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Could not get interaction for {InteractionName} and ClientId {ClientId}",
                            kvp.Key,
                            clientId);
                        return null;
                    }
                });

        return (await Task.WhenAll(interactions))
            .Where(interaction => interaction is not null)
            .ToList();
    }
}
