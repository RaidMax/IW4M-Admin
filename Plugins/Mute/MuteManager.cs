using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using static System.Enum;

namespace Mute;

public class MuteManager
{
    private readonly IMetaServiceV2 _metaService;
    private readonly ITranslationLookup _translationLookup;
    private List<MuteStateMeta> _muteList = null!;

    public MuteManager(IMetaServiceV2 metaService, ITranslationLookup translationLookup)
    {
        _metaService = metaService;
        _translationLookup = translationLookup;
    }

    public async Task InitialiseMuteList() => _muteList = await ReadMuteList();
    public bool HasMutes => _muteList.Any(mute => mute.Expiration > DateTime.UtcNow || mute.Expiration == null);

    public IEnumerable<MuteStateMeta> Mutes =>
        _muteList.Where(mute => mute.Expiration > DateTime.UtcNow || mute.Expiration == null);

    public static bool IsExpiredMute(MuteStateMeta muteStateMeta) =>
        muteStateMeta.Expiration is not null && muteStateMeta.Expiration < DateTime.UtcNow;

    public async Task<MuteStateMeta> GetCurrentMuteState(EFClient client)
    {
        var clientMuteMeta = await ReadPersistentDataV2(client);
        if (clientMuteMeta is not null) return clientMuteMeta;

        // Return null if the client doesn't have old or new meta.
        var muteState = await ReadPersistentDataV1(client);
        clientMuteMeta = new MuteStateMeta
        {
            ClientId = client.ClientId,
            CleanedName = client.CleanedName,
            Reason = muteState is null ? string.Empty : _translationLookup["PLUGINS_MUTE_MIGRATED"],
            Expiration = muteState switch
            {
                null => DateTime.UtcNow,
                MuteState.Muted => null,
                _ => DateTime.UtcNow
            },
            AdminId = Utilities.IW4MAdminClient().ClientId,
            MuteState = muteState ?? MuteState.Unmuted,
            CommandExecuted = true
        };

        // Migrate old mute meta, else, client has no state, so set a generic one, but don't write it to database.
        if (muteState is not null)
        {
            await WritePersistentData(client, clientMuteMeta);
            await UpdateMuteList(clientMuteMeta);
        }
        else
        {
            client.SetAdditionalProperty(Plugin.MuteKey, clientMuteMeta);
        }

        return clientMuteMeta;
    }

    public async Task<bool> Mute(Server server, EFClient origin, EFClient target, DateTime? dateTime, string reason)
    {
        var clientMuteMeta = await GetCurrentMuteState(target);
        if (clientMuteMeta.MuteState is MuteState.Muted && clientMuteMeta.CommandExecuted) return false;

        clientMuteMeta = new MuteStateMeta
        {
            ClientId = target.ClientId,
            CleanedName = target.CleanedName,
            Expiration = dateTime,
            MuteState = MuteState.Muted,
            Reason = reason,
            AdminId = origin.ClientId,
            CommandExecuted = false
        };
        await WritePersistentData(target, clientMuteMeta);

        // Handle mute list
        await UpdateMuteList(clientMuteMeta);

        // Handle game command
        var client = server.GetClientsAsList().FirstOrDefault(client => client.NetworkId == target.NetworkId);
        await PerformGameCommand(server, client, clientMuteMeta);

        return true;
    }

    public async Task<bool> Unmute(Server server, EFClient origin, EFClient target, string reason)
    {
        var clientMuteMeta = await GetCurrentMuteState(target);
        if (clientMuteMeta.MuteState is MuteState.Unmuted && clientMuteMeta.CommandExecuted) return false;
        if (!target.IsIngame && clientMuteMeta.MuteState is MuteState.Unmuting) return false;

        clientMuteMeta = new MuteStateMeta
        {
            ClientId = target.ClientId,
            CleanedName = target.CleanedName,
            Expiration = DateTime.UtcNow,
            MuteState = target.IsIngame ? MuteState.Unmuted : MuteState.Unmuting,
            Reason = reason,
            AdminId = origin.ClientId,
            CommandExecuted = false
        };
        await WritePersistentData(target, clientMuteMeta);

        // Handle mute list
        await UpdateMuteList(clientMuteMeta);

        // Handle game command
        var client = server.GetClientsAsList().FirstOrDefault(client => client.NetworkId == target.NetworkId);
        await PerformGameCommand(server, client, clientMuteMeta);

        return true;
    }

    private async Task UpdateMuteList(MuteStateMeta muteStateMeta)
    {
        var muteListEntry = _muteList.FirstOrDefault(mute => mute.ClientId == muteStateMeta.ClientId);
        lock (_muteList)
        {
            if (muteStateMeta.MuteState == MuteState.Muted)
            {
                if (muteListEntry is null) _muteList.Add(muteStateMeta);
            }
            else
            {
                if (muteListEntry is not null) _muteList.Remove(muteListEntry);
            }
        }

        await WriteMuteList(_muteList);
    }

    public static async Task PerformGameCommand(Server server, EFClient? client, MuteStateMeta muteStateMeta)
    {
        if (client is null || !client.IsIngame) return;

        switch (muteStateMeta.MuteState)
        {
            case MuteState.Muted:
                await server.ExecuteCommandAsync($"muteClient {client.ClientNumber}");
                muteStateMeta.CommandExecuted = true;
                break;
            case MuteState.Unmuted:
                await server.ExecuteCommandAsync($"unmute {client.ClientNumber}");
                muteStateMeta.CommandExecuted = true;
                break;
        }
    }

    private async Task<MuteState?> ReadPersistentDataV1(EFClient client) => TryParse<MuteState>(
        (await _metaService.GetPersistentMeta(Plugin.MuteKey, client.ClientId))?.Value,
        out var muteState)
        ? muteState
        : null;

    private async Task<MuteStateMeta?> ReadPersistentDataV2(EFClient client)
    {
        // Get meta from client
        var clientMuteMeta = client.GetAdditionalProperty<MuteStateMeta>(Plugin.MuteKey);
        if (clientMuteMeta is not null) return clientMuteMeta;

        // Get meta from database and store in client if exists
        clientMuteMeta = await _metaService.GetPersistentMetaValue<MuteStateMeta>(Plugin.MuteKey, client.ClientId);
        if (clientMuteMeta is not null) client.SetAdditionalProperty(Plugin.MuteKey, clientMuteMeta);

        return clientMuteMeta;
    }

    private async Task WritePersistentData(EFClient client, MuteStateMeta clientMuteMeta)
    {
        client.SetAdditionalProperty(Plugin.MuteKey, clientMuteMeta);
        await _metaService.SetPersistentMetaValue(Plugin.MuteKey, clientMuteMeta, client.ClientId);
    }

    private async Task<List<MuteStateMeta>> ReadMuteList() =>
        await _metaService.GetPersistentMetaValue<List<MuteStateMeta>>(Plugin.MuteListKey) ?? new List<MuteStateMeta>();

    private async Task WriteMuteList(List<MuteStateMeta> muteList) =>
        await _metaService.SetPersistentMetaValue(Plugin.MuteListKey, muteList);
}
