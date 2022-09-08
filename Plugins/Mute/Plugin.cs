using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;

namespace Mute;

public class Plugin : IPlugin
{
    private readonly IInteractionRegistration _interactionRegistration;
    private static readonly string MuteInteraction = nameof(MuteInteraction);

    public Plugin(IMetaServiceV2 metaService, IInteractionRegistration interactionRegistration)
    {
        _interactionRegistration = interactionRegistration;
        DataManager = new DataManager(metaService);
    }

    public string Name => "Mute";
    public float Version => (float) Utilities.GetVersionAsDouble();
    public string Author => "Amos";

    public static string MuteKey = "IW4MMute";

    public static DataManager DataManager;
    public static readonly Server.Game[] SupportedGames = {Server.Game.IW4};

    public async Task OnEventAsync(GameEvent gameEvent, Server server)
    {
        if (!SupportedGames.Contains(server.GameName)) return;

        switch (gameEvent.Type)
        {
            case GameEvent.EventType.Join:
                switch (await DataManager.ReadPersistentData(gameEvent.Origin))
                {
                    case MuteState.Muted:
                        await server.ExecuteCommandAsync($"muteClient {gameEvent.Origin.ClientNumber}");
                        break;
                    case MuteState.Unmuting:
                        await server.ExecuteCommandAsync($"unmute {gameEvent.Origin.ClientNumber}");
                        await DataManager.WritePersistentData(gameEvent.Origin, MuteState.Unmuted);
                        break;
                    case MuteState.Unmuted:
                        break;
                }

                break;
        }
    }

    public Task OnLoadAsync(IManager manager)
    {
        _interactionRegistration.RegisterInteraction(MuteInteraction, async (clientId, game, token) =>
        {
            if (!clientId.HasValue || game.HasValue && !SupportedGames.Contains((Server.Game)game.Value))
            {
                return null;
            }

            var muteState = await DataManager.ReadPersistentData(new EFClient { ClientId = clientId.Value });

            return muteState is MuteState.Unmuted or MuteState.Unmuting
                ? new InteractionData
                {
                    EntityId = clientId,
                    Name = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_MUTE"],
                    DisplayMeta = "oi-volume-off",
                    ActionPath = "DynamicAction",
                    ActionMeta = new()
                    {
                        { "InteractionId", "command" },
                        { "Data", $"mute @{clientId.Value}" },
                        { "ActionButtonLabel", Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_MUTE"] },
                        { "Name", Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_MUTE"] },
                        { "ShouldRefresh", true.ToString() }
                    },
                    MinimumPermission = Data.Models.Client.EFClient.Permission.Moderator,
                    Source = Name
                }
                : new InteractionData
                {
                    EntityId = clientId,
                    Name = Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_UNMUTE"],
                    DisplayMeta = "oi-volume-high",
                    ActionPath = "DynamicAction",
                    ActionMeta = new()
                    {
                        { "InteractionId", "command" },
                        { "Data", $"mute @{clientId.Value}" },
                        { "ActionButtonLabel", Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_UNMUTE"] },
                        { "Name", Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_UNMUTE"] },
                        { "ShouldRefresh", true.ToString() }
                    },
                    MinimumPermission = Data.Models.Client.EFClient.Permission.Moderator,
                    Source = Name
                };
        });

        return Task.CompletedTask;
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
