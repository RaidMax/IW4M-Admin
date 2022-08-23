using Data.Models.Client;
using SharedLibraryCore.Interfaces;
using static System.Enum;

namespace Mute;

public class DataManager
{
    public DataManager(IMetaServiceV2 metaService)
    {
        _metaService = metaService;
    }

    private readonly IMetaServiceV2 _metaService;

    public async Task<MuteState> ReadPersistentData(EFClient client)
    {
        var clientMuteState = client.GetAdditionalProperty<MuteState?>(Plugin.MuteKey) ??
                              Parse<MuteState>((await _metaService.GetPersistentMeta(Plugin.MuteKey, client.ClientId))?
                                  .Value ?? nameof(MuteState.Unmuted));

        client.SetAdditionalProperty(Plugin.MuteKey, clientMuteState);
        return clientMuteState;
    }

    public async Task WritePersistentData(EFClient client, MuteState state)
    {
        await _metaService.SetPersistentMeta(Plugin.MuteKey, state.ToString(), client.ClientId);
        client.SetAdditionalProperty(Plugin.MuteKey, state);
    }
}

public enum MuteState
{
    Muted,
    Unmuting,
    Unmuted
}
