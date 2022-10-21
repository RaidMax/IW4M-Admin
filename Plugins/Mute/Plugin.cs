using Data.Abstractions;
using Microsoft.Extensions.Logging;
using Mute.Commands;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Mute;

public class Plugin : IPlugin
{
    public string Name => "Mute";
    public float Version => (float)Utilities.GetVersionAsDouble();
    public string Author => "Amos";

    public const string MuteKey = "IW4MMute";
    public static MuteManager MuteManager { get; private set; } = null!;
    public static IManager Manager { get; private set; } = null!;
    public static readonly Server.Game[] SupportedGames = {Server.Game.IW4};
    private static readonly string[] DisabledCommands = {nameof(PrivateMessageAdminsCommand), "PrivateMessageCommand"};
    private readonly IInteractionRegistration _interactionRegistration;
    private readonly IRemoteCommandService _remoteCommandService;
    private static readonly string MuteInteraction = "Webfront::Profile::Mute";

    public Plugin(ILogger<Plugin> logger, IInteractionRegistration interactionRegistration,
        IRemoteCommandService remoteCommandService, IServiceProvider serviceProvider)
    {
        _interactionRegistration = interactionRegistration;
        _remoteCommandService = remoteCommandService;
        MuteManager = new MuteManager(serviceProvider);
    }

    public async Task OnEventAsync(GameEvent gameEvent, Server server)
    {
        if (!SupportedGames.Contains(server.GameName)) return;

        switch (gameEvent.Type)
        {
            case GameEvent.EventType.Join:
                // Check if user has any meta set, else ignore (unmuted)
                var muteMetaJoin = await MuteManager.GetCurrentMuteState(gameEvent.Origin);

                switch (muteMetaJoin.MuteState)
                {
                    case MuteState.Muted:
                        // Let the client know when their mute expires.
                        gameEvent.Origin.Tell(Utilities.CurrentLocalization
                            .LocalizationIndex["PLUGINS_MUTE_REMAINING_TIME"].FormatExt(
                                muteMetaJoin.Expiration is not null
                                    ? muteMetaJoin.Expiration.Value.HumanizeForCurrentCulture()
                                    : Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_MUTE_NEVER"],
                                muteMetaJoin.Reason));
                        break;
                    case MuteState.Unmuting:
                        // Handle unmute of unmuted players.
                        await MuteManager.Unmute(server, Utilities.IW4MAdminClient(), gameEvent.Origin,
                            muteMetaJoin.Reason);
                        gameEvent.Origin.Tell(Utilities.CurrentLocalization
                            .LocalizationIndex["PLUGINS_MUTE_COMMANDS_UNMUTE_TARGET_UNMUTED"]
                            .FormatExt(muteMetaJoin.Reason));
                        break;
                }

                break;
            case GameEvent.EventType.Say:
                var muteMetaSay = await MuteManager.GetCurrentMuteState(gameEvent.Origin);

                switch (muteMetaSay.MuteState)
                {
                    case MuteState.Muted:
                        // Let the client know when their mute expires.
                        gameEvent.Origin.Tell(Utilities.CurrentLocalization
                            .LocalizationIndex["PLUGINS_MUTE_REMAINING_TIME"].FormatExt(
                                muteMetaSay.Expiration is not null
                                    ? muteMetaSay.Expiration.Value.HumanizeForCurrentCulture()
                                    : Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_MUTE_NEVER"],
                                muteMetaSay.Reason));
                        break;
                }

                break;
            case GameEvent.EventType.Update:
                // Get correct EFClient object
                var client = server.GetClientsAsList()
                    .FirstOrDefault(client => client.NetworkId == gameEvent.Origin.NetworkId);
                if (client == null) break;

                var muteMetaUpdate = await MuteManager.GetCurrentMuteState(client);
                if (!muteMetaUpdate.CommandExecuted)
                {
                    await MuteManager.PerformGameCommand(server, client, muteMetaUpdate);
                }

                switch (muteMetaUpdate.MuteState)
                {
                    case MuteState.Muted:
                        // Handle unmute if expired.
                        if (MuteManager.IsExpiredMute(muteMetaUpdate))
                        {
                            await MuteManager.Unmute(server, Utilities.IW4MAdminClient(), client,
                                Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_MUTE_EXPIRED"]);
                            client.Tell(
                                Utilities.CurrentLocalization.LocalizationIndex["PLUGINS_MUTE_TARGET_EXPIRED"]);
                        }

                        break;
                }

                break;
        }
    }

    public Task OnLoadAsync(IManager manager)
    {
        Manager = manager;

        manager.CommandInterceptors.Add(gameEvent =>
        {
            if (gameEvent.Extra is not Command command)
            {
                return true;
            }

            var muteMeta = MuteManager.GetCurrentMuteState(gameEvent.Origin).GetAwaiter().GetResult();
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
                (await MuteManager.GetCurrentMuteState(new EFClient {ClientId = targetClientId.Value}))
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

    public Task OnUnloadAsync()
    {
        _interactionRegistration.UnregisterInteraction(MuteInteraction);
        return Task.CompletedTask;
    }

    public Task OnTickAsync(Server server)
    {
        return Task.CompletedTask;
    }
}
