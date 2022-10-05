using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Interfaces;

namespace Mute;

public class Plugin : IPlugin
{
    public string Name => "Mute";
    public float Version => (float) Utilities.GetVersionAsDouble();
    public string Author => "Amos";

    public const string MuteKey = "IW4MMute";
    public const string MuteListKey = "IW4MMuteList";
    public static MuteManager MuteManager { get; private set; } = null!;
    public static readonly Server.Game[] SupportedGames = {Server.Game.IW4};
    private static readonly string[] DisabledCommands = {nameof(PrivateMessageAdminsCommand), "PrivateMessageCommand"};
    private readonly IInteractionRegistration _interactionRegistration;
    private static readonly string MuteInteraction = nameof(MuteInteraction);

    public Plugin(IMetaServiceV2 metaService, IInteractionRegistration interactionRegistration,
        ITranslationLookup translationLookup)
    {
        _interactionRegistration = interactionRegistration;
        MuteManager = new MuteManager(metaService, translationLookup);
    }

    public async Task OnEventAsync(GameEvent gameEvent, Server server)
    {
        if (!SupportedGames.Contains(server.GameName)) return;


        switch (gameEvent.Type)
        {
            case GameEvent.EventType.Command:

                break;
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

    public async Task OnLoadAsync(IManager manager)
    {
        manager.CommandInterceptors.Add(gameEvent =>
        {
            if (gameEvent.Extra is not Command command) return true;
            return !DisabledCommands.Contains(command.GetType().Name) && !command.IsBroadcast;
        });
        
        await MuteManager.InitialiseMuteList();

        // TODO: Implement reason dialogue and tempmute dialogues. 
        //_interactionRegistration.RegisterInteraction(MuteInteraction, async (clientId, game, token) =>
        //{
        //    if (!clientId.HasValue || game.HasValue && !SupportedGames.Contains((Server.Game) game.Value))
        //    {
        //        return null;
        //    }
        //    // TODO: THIS HAS NO WRITE ACTION CURRENTLY. MUTING FROM WEB THEM WILL DO NOTHING.
        //    var clientMuteMetaState =
        //        (await MuteManager.GetCurrentMuteState(new EFClient {ClientId = clientId.Value}))?.MuteState ??
        //        MuteState.Unmuted;
        //    return clientMuteMetaState is MuteState.Unmuted or MuteState.Unmuting
        //        ? new InteractionData
        //        {
        //            EntityId = clientId,
        //            Name = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_MUTE"],
        //            DisplayMeta = "oi-volume-off",
        //            ActionPath = "DynamicAction",
        //            ActionMeta = new()
        //            {
        //                {"InteractionId", "command"},
        //                {"Data", $"mute @{clientId.Value}"},
        //                {
        //                    "ActionButtonLabel",
        //                    Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_MUTE"]
        //                },
        //                {
        //                    "Name",
        //                    Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_MUTE"]
        //                },
        //                {"ShouldRefresh", true.ToString()}
        //            },
        //            MinimumPermission = Data.Models.Client.EFClient.Permission.Moderator,
        //            Source = Name
        //        }
        //        : new InteractionData
        //        {
        //            EntityId = clientId,
        //            Name = Utilities.CurrentLocalization.LocalizationIndex[
        //                "WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_UNMUTE"],
        //            DisplayMeta = "oi-volume-high",
        //            ActionPath = "DynamicAction",
        //            ActionMeta = new()
        //            {
        //                {"InteractionId", "command"},
        //                {"Data", $"mute @{clientId.Value}"},
        //                {
        //                    "ActionButtonLabel",
        //                    Utilities.CurrentLocalization.LocalizationIndex[
        //                        "WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_UNMUTE"]
        //                },
        //                {
        //                    "Name",
        //                    Utilities.CurrentLocalization.LocalizationIndex[
        //                        "WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_UNMUTE"]
        //                },
        //                {"ShouldRefresh", true.ToString()}
        //            },
        //            MinimumPermission = Data.Models.Client.EFClient.Permission.Moderator,
        //            Source = Name
        //        };
        //});
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
