using IW4MAdmin.Plugins.Mute.Commands;
using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Events.Server;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace IW4MAdmin.Plugins.Mute;

public class Plugin : IPluginV2
{
    public string Name => "Mute";
    public string Version => Utilities.GetVersionAsString();
    public string Author => "Amos";

    public const string MuteKey = "IW4MMute";
    public static IManager Manager { get; private set; } = null!;
    public static readonly Server.Game[] SupportedGames = {Server.Game.IW4};
    private static readonly string[] DisabledCommands = {nameof(PrivateMessageAdminsCommand), "PrivateMessageCommand"};
    private readonly IInteractionRegistration _interactionRegistration;
    private readonly IRemoteCommandService _remoteCommandService;
    private readonly MuteManager _muteManager;
    private const string MuteInteraction = "Webfront::Profile::Mute";

    public Plugin(IInteractionRegistration interactionRegistration,
        IRemoteCommandService remoteCommandService, MuteManager muteManager)
    {
        _interactionRegistration = interactionRegistration;
        _remoteCommandService = remoteCommandService;
        _muteManager = muteManager;
        
        IManagementEventSubscriptions.Load += OnLoad;
        IManagementEventSubscriptions.Unload += OnUnload;
        
        IManagementEventSubscriptions.ClientStateInitialized += OnClientStateInitialized;
        IGameServerEventSubscriptions.ClientDataUpdated += OnClientDataUpdated;
        IGameEventSubscriptions.ClientMessaged += OnClientMessaged;
    }

    public static void RegisterDependencies(IServiceCollection serviceProvider)
    {
        serviceProvider.AddSingleton<MuteManager>();
    }

    private Task OnLoad(IManager manager, CancellationToken cancellationToken)
    {
        Manager = manager;

        manager.CommandInterceptors.Add(gameEvent =>
        {
            if (gameEvent.Extra is not Command command)
            {
                return true;
            }

            var muteMeta = _muteManager.GetCurrentMuteState(gameEvent.Origin).GetAwaiter().GetResult();
            if (muteMeta.MuteState is not MuteState.Muted)
            {
                return true;
            }

            return !DisabledCommands.Contains(command.GetType().Name) && !command.IsBroadcast;
        });

        _interactionRegistration.RegisterInteraction(MuteInteraction, async (targetClientId, game, token) =>
        {
            if (!targetClientId.HasValue || game.HasValue && !SupportedGames.Contains((Server.Game)game.Value))
            {
                return null;
            }

            var clientMuteMetaState =
                (await _muteManager.GetCurrentMuteState(new EFClient {ClientId = targetClientId.Value}))
                .MuteState;
            var server = manager.GetServers().First();

            string GetCommandName(Type commandType) =>
                manager.Commands.FirstOrDefault(command => command.GetType() == commandType)?.Name ?? "";

            return clientMuteMetaState is MuteState.Unmuted or MuteState.Unmuting
                ? CreateMuteInteraction(targetClientId.Value, server, GetCommandName)
                : CreateUnmuteInteraction(targetClientId.Value, server, GetCommandName);
        });
        return Task.CompletedTask;
    }
    
    private Task OnUnload(IManager manager, CancellationToken token)
    {
        _interactionRegistration.UnregisterInteraction(MuteInteraction);
        return Task.CompletedTask;
    }

    private async Task OnClientDataUpdated(ClientDataUpdateEvent updateEvent, CancellationToken token)
    {
        if (!updateEvent.Server.ConnectedClients.Any())
        {
            return;
        }

        var networkIds = updateEvent.Clients.Select(client => client.NetworkId).ToList();
        var ingameClients = updateEvent.Server.ConnectedClients.Where(client => networkIds.Contains(client.NetworkId));

        await Task.WhenAll(ingameClients.Select(async client =>
        {
            var muteMetaUpdate = await _muteManager.GetCurrentMuteState(client);
            if (!muteMetaUpdate.CommandExecuted)
            {
                await MuteManager.PerformGameCommand(client.CurrentServer, client, muteMetaUpdate);
            }

            if (muteMetaUpdate.MuteState == MuteState.Muted)
            {
                // Handle unmute if expired.
                if (MuteManager.IsExpiredMute(muteMetaUpdate))
                {
                    await _muteManager.Unmute(client.CurrentServer, Utilities.IW4MAdminClient(), client,
                        Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_MUTE_EXPIRED"]);
                    client.Tell(
                        Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_MUTE_TARGET_EXPIRED"]);
                }
            }
        }));
    }

    private async Task OnClientMessaged(ClientMessageEvent messageEvent, CancellationToken token)
    {
        var muteMetaSay = await _muteManager.GetCurrentMuteState(messageEvent.Origin);

        if (muteMetaSay.MuteState == MuteState.Muted)
        {
            // Let the client know when their mute expires.
            messageEvent.Origin.Tell(Utilities.CurrentLocalization
                .LocalizationIndex["PLUGINS_MUTE_REMAINING_TIME"].FormatExt(
                    muteMetaSay.Expiration is not null
                        ? muteMetaSay.Expiration.Value.HumanizeForCurrentCulture()
                        : Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_MUTE_NEVER"],
                    muteMetaSay.Reason));
        }
    }

    private async Task OnClientStateInitialized(ClientStateInitializeEvent state, CancellationToken token)
    {
        if (!SupportedGames.Contains(state.Client.CurrentServer.GameName))
        {
            return;
        }
        
        var muteMetaJoin = await _muteManager.GetCurrentMuteState(state.Client);

        switch (muteMetaJoin)
        {
            case { MuteState: MuteState.Muted }:
                // Let the client know when their mute expires.
                state.Client.Tell(Utilities.CurrentLocalization
                    .LocalizationIndex["PLUGINS_MUTE_REMAINING_TIME"].FormatExt(
                        muteMetaJoin is { Expiration: not null }
                            ? muteMetaJoin.Expiration.Value.HumanizeForCurrentCulture()
                            : Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_MUTE_NEVER"],
                        muteMetaJoin.Reason));
                break;
            case { MuteState: MuteState.Unmuting }:
                // Handle unmute of unmuted players.
                await _muteManager.Unmute(state.Client.CurrentServer, Utilities.IW4MAdminClient(), state.Client,
                    muteMetaJoin.Reason ?? string.Empty);
                state.Client.Tell(Utilities.CurrentLocalization
                    .LocalizationIndex["PLUGINS_MUTE_COMMANDS_UNMUTE_TARGET_UNMUTED"]
                    .FormatExt(muteMetaJoin.Reason));
                break;
        }
    }

    private InteractionData CreateMuteInteraction(int targetClientId, Server server,
        Func<Type, string> getCommandNameFunc)
    {
        var reasonInput = new
        {
            Name = "Reason",
            Label = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_ACTION_LABEL_REASON"],
            Type = "text",
            Values = (Dictionary<string, string>?)null
        };

        var durationInput = new
        {
            Name = "Duration",
            Label = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_ACTION_LABEL_DURATION"],
            Type = "select",
            Values = (Dictionary<string, string>?)new Dictionary<string, string>
            {
                {"5m", TimeSpan.FromMinutes(5).HumanizeForCurrentCulture()},
                {"30m", TimeSpan.FromMinutes(30).HumanizeForCurrentCulture()},
                {"1h", TimeSpan.FromHours(1).HumanizeForCurrentCulture()},
                {"6h", TimeSpan.FromHours(6).HumanizeForCurrentCulture()},
                {"1d", TimeSpan.FromDays(1).HumanizeForCurrentCulture()},
                {"p", Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_ACTION_SELECTION_PERMANENT"]}
            }
        };

        var inputs = new[] {reasonInput, durationInput};
        var inputsJson = JsonSerializer.Serialize(inputs);

        return new InteractionData
        {
            EntityId = targetClientId,
            Name = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_MUTE"],
            DisplayMeta = "oi-volume-off",
            ActionPath = "DynamicAction",
            ActionMeta = new()
            {
                {"InteractionId", MuteInteraction},
                {"Inputs", inputsJson},
                {
                    "ActionButtonLabel",
                    Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_MUTE"]
                },
                {
                    "Name",
                    Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_MUTE"]
                },
                {"ShouldRefresh", true.ToString()}
            },
            MinimumPermission = Data.Models.Client.EFClient.Permission.Moderator,
            Source = Name,
            Action = async (originId, targetId, gameName, meta, cancellationToken) =>
            {
                if (!targetId.HasValue)
                {
                    return "No target client id specified";
                }

                var isTempMute = meta.ContainsKey(durationInput.Name) &&
                                 meta[durationInput.Name] != durationInput.Values?.Last().Key;
                var muteCommand = getCommandNameFunc(isTempMute ? typeof(TempMuteCommand) : typeof(MuteCommand));
                var args = new List<string>();

                if (meta.TryGetValue(durationInput.Name, out var duration) &&
                    duration != durationInput.Values?.Last().Key)
                {
                    args.Add(duration);
                }

                if (meta.TryGetValue(reasonInput.Name, out var reason))
                {
                    args.Add(reason);
                }

                var commandResponse =
                    await _remoteCommandService.Execute(originId, targetId, muteCommand, args, server);
                return string.Join(".", commandResponse.Select(result => result.Response));
            }
        };
    }

    private InteractionData CreateUnmuteInteraction(int targetClientId, Server server,
        Func<Type, string> getCommandNameFunc)
    {
        var reasonInput = new
        {
            Name = "Reason",
            Label = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_ACTION_LABEL_REASON"],
            Type = "text",
        };

        var inputs = new[] {reasonInput};
        var inputsJson = JsonSerializer.Serialize(inputs);

        return new InteractionData
        {
            EntityId = targetClientId,
            Name = Utilities.CurrentLocalization.LocalizationIndex[
                "WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_UNMUTE"],
            DisplayMeta = "oi-volume-high",
            ActionPath = "DynamicAction",
            ActionMeta = new()
            {
                {"InteractionId", MuteInteraction},
                {"Outputs", reasonInput.Name},
                {"Inputs", inputsJson},
                {
                    "ActionButtonLabel",
                    Utilities.CurrentLocalization.LocalizationIndex[
                        "WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_UNMUTE"]
                },
                {
                    "Name",
                    Utilities.CurrentLocalization.LocalizationIndex[
                        "WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_UNMUTE"]
                },
                {"ShouldRefresh", true.ToString()}
            },
            MinimumPermission = Data.Models.Client.EFClient.Permission.Moderator,
            Source = Name,
            Action = async (originId, targetId, gameName, meta, cancellationToken) =>
            {
                if (!targetId.HasValue)
                {
                    return "No target client id specified";
                }

                var args = new List<string>();

                if (meta.TryGetValue(reasonInput.Name, out var reason))
                {
                    args.Add(reason);
                }

                var commandResponse =
                    await _remoteCommandService.Execute(originId, targetId, getCommandNameFunc(typeof(UnmuteCommand)),
                        args, server);
                return string.Join(".", commandResponse.Select(result => result.Response));
            }
        };
    }
}
