using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;
using WebfrontCore.Core.Auth;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Misc;

public class MetaServiceV2 : IMetaServiceV2
{
    protected class MetaRegistration
    {
        public required dynamic Action;
        public required WebfrontEntity EntityType;
    }
    
    private readonly IDictionary<MetaType, List<MetaRegistration>> _metaActions;
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly ApplicationConfiguration _configuration;
    private readonly ILogger _logger;

    public MetaServiceV2(ILogger<MetaServiceV2> logger, IDatabaseContextFactory contextFactory,
        ApplicationConfiguration configuration)
    {
        _logger = logger;
        _metaActions = new Dictionary<MetaType, List<MetaRegistration>>();
        _contextFactory = contextFactory;
        _configuration = configuration;
    }

    public async Task SetPersistentMeta(string metaKey, string metaValue, int clientId,
        CancellationToken token = default)
    {
        if (!ValidArgs(metaKey, clientId))
        {
            return;
        }

        await using var context = _contextFactory.CreateContext();

        var existingMeta = await context.EFMeta
            .Where(meta => meta.Key == metaKey)
            .Where(meta => meta.ClientId == clientId)
            .FirstOrDefaultAsync(token);

        if (existingMeta != null)
        {
            _logger.LogDebug("Updating existing meta with key {Key} and id {Id}", existingMeta.Key,
                existingMeta.MetaId);
            existingMeta.Value = metaValue;
            existingMeta.Updated = DateTime.UtcNow;
        }

        else
        {
            _logger.LogDebug("Adding new meta with key {Key}", metaKey);
            context.EFMeta.Add(new EFMeta
            {
                ClientId = clientId,
                Created = DateTime.UtcNow,
                Key = metaKey,
                Value = metaValue,
            });
        }

        await context.SaveChangesAsync(token);
    }

    public async Task SetPersistentMetaValue<T>(string metaKey, T metaValue, int clientId,
        CancellationToken token = default) where T : class
    {
        if (!ValidArgs(metaKey, clientId))
        {
            return;
        }

        string serializedValue;

        try
        {
            serializedValue = JsonSerializer.Serialize(metaValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not serialize meta with key {Key}", metaKey);
            return;
        }

        await SetPersistentMeta(metaKey, serializedValue, clientId, token);
    }

    public async Task SetPersistentMetaForLookupKey(string metaKey, string lookupKey, int lookupId, int clientId,
        CancellationToken token = default)
    {
        if (!ValidArgs(metaKey, clientId))
        {
            return;
        }

        await using var context = _contextFactory.CreateContext();

        var lookupMeta = await context.EFMeta.FirstOrDefaultAsync(meta => meta.Key == lookupKey, token);

        if (lookupMeta is null)
        {
            _logger.LogWarning("No lookup meta exists for metaKey {MetaKey} and lookupKey {LookupKey}", metaKey,
                lookupKey);
            return;
        }

        var lookupValues = JsonSerializer.Deserialize<List<LookupValue<string>>>(lookupMeta.Value);

        if (lookupValues is null)
        {
            return;
        }

        var foundLookup = lookupValues.FirstOrDefault(value => value.Id == lookupId);

        if (foundLookup is null)
        {
            _logger.LogWarning("No lookup meta found for provided lookup id {MetaKey}, {LookupKey}, {LookupId}",
                metaKey, lookupKey, lookupId);
            return;
        }

        _logger.LogDebug("Setting meta for lookup {MetaKey}, {LookupKey}, {LookupId}",
            metaKey, lookupKey, lookupId);

        await SetPersistentMeta(metaKey, lookupId.ToString(), clientId, token);
    }

    public async Task IncrementPersistentMeta(string metaKey, int incrementAmount, int clientId,
        CancellationToken token = default)
    {
        if (!ValidArgs(metaKey, clientId))
        {
            return;
        }

        var existingMeta = await GetPersistentMeta(metaKey, clientId, token);

        if (!long.TryParse(existingMeta?.Value ?? "0", out var existingValue))
        {
            existingValue = 0;
        }

        var newValue = existingValue + incrementAmount;
        await SetPersistentMeta(metaKey, newValue.ToString(), clientId, token);
    }

    public async Task DecrementPersistentMeta(string metaKey, int decrementAmount, int clientId,
        CancellationToken token = default)
    {
        await IncrementPersistentMeta(metaKey, -decrementAmount, clientId, token);
    }

    public async Task<EFMeta> GetPersistentMeta(string metaKey, int clientId, CancellationToken token = default)
    {
        if (!ValidArgs(metaKey, clientId))
        {
            return null;
        }

        await using var ctx = _contextFactory.CreateContext(enableTracking: false);

        return await ctx.EFMeta
            .Where(meta => meta.Key == metaKey)
            .Where(meta => meta.ClientId == clientId)
            .Select(meta => new EFMeta
            {
                MetaId = meta.MetaId,
                Key = meta.Key,
                ClientId = meta.ClientId,
                Value = meta.Value,
            })
            .FirstOrDefaultAsync(token);
    }

    public async Task<T> GetPersistentMetaValue<T>(string metaKey, int clientId, CancellationToken token = default)
        where T : class
    {
        var meta = await GetPersistentMeta(metaKey, clientId, token);

        if (meta is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(meta.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not deserialize meta with key {Key} and value {Value}", metaKey, meta.Value);
            return null;
        }
    }

    public async Task<EFMeta> GetPersistentMetaByLookup(string metaKey, string lookupKey, int clientId,
        CancellationToken token = default)
    {
        await using var context = _contextFactory.CreateContext();

        var metaValue = await GetPersistentMeta(metaKey, clientId, token);

        if (metaValue is null)
        {
            _logger.LogDebug("No meta exists for key {Key}, clientId {ClientId}", metaKey, clientId);
            return null;
        }

        var lookupMeta = await context.EFMeta.FirstOrDefaultAsync(meta => meta.Key == lookupKey, token);

        if (lookupMeta is null)
        {
            _logger.LogWarning("No lookup meta exists for metaKey {MetaKey} and lookupKey {LookupKey}", metaKey,
                lookupKey);
            return null;
        }

        var lookupId = int.Parse(metaValue.Value);
        var lookupValues = JsonSerializer.Deserialize<List<LookupValue<string>>>(lookupMeta.Value);

        if (lookupValues is null)
        {
            return null;
        }

        var foundLookup = lookupValues.FirstOrDefault(value => value.Id == lookupId);

        if (foundLookup is not null)
        {
            return new EFMeta
            {
                Created = metaValue.Created,
                Updated = metaValue.Updated,
                Extra = metaValue.Extra,
                MetaId = metaValue.MetaId,
                Value = foundLookup.Value
            };
        }

        _logger.LogWarning("No lookup meta found for provided lookup id {MetaKey}, {LookupKey}, {LookupId}",
            metaKey, lookupKey, lookupId);
        return null;
    }

    public async Task RemovePersistentMeta(string metaKey, int clientId, CancellationToken token = default)
    {
        if (!ValidArgs(metaKey, clientId))
        {
            return;
        }

        await using var context = _contextFactory.CreateContext();

        var existingMeta = await context.EFMeta
            .FirstOrDefaultAsync(meta => meta.Key == metaKey && meta.ClientId == clientId, token);

        if (existingMeta == null)
        {
            _logger.LogDebug("No meta with key {Key} found for client id {Id}", metaKey, clientId);
            return;
        }

        _logger.LogDebug("Removing meta for key {Key} with id {Id}", metaKey, existingMeta.MetaId);
        context.EFMeta.Remove(existingMeta);
        await context.SaveChangesAsync(token);
    }

    public async Task SetPersistentMeta(string metaKey, string metaValue, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(metaKey))
        {
            _logger.LogWarning("Cannot save meta with no key");
            return;
        }

        await using var ctx = _contextFactory.CreateContext();

        var existingMeta = await ctx.EFMeta
            .Where(meta => meta.Key == metaKey)
            .Where(meta => meta.ClientId == null)
            .FirstOrDefaultAsync(token);

        if (existingMeta is not null)
        {
            _logger.LogDebug("Updating existing meta with key {Key} and id {Id}", existingMeta.Key,
                existingMeta.MetaId);

            existingMeta.Value = metaValue;
            existingMeta.Updated = DateTime.UtcNow;

            await ctx.SaveChangesAsync(token);
        }

        else
        {
            _logger.LogDebug("Adding new meta with key {Key}", metaKey);

            ctx.EFMeta.Add(new EFMeta
            {
                Created = DateTime.UtcNow,
                Key = metaKey,
                Value = metaValue
            });

            await ctx.SaveChangesAsync(token);
        }
    }

    public async Task SetPersistentMetaValue<T>(string metaKey, T metaValue, CancellationToken token = default)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(metaKey))
        {
            _logger.LogWarning("Meta key is null, not setting");
            return;
        }

        if (metaValue is null)
        {
            _logger.LogWarning("Meta value is null, not setting");
            return;
        }

        string serializedMeta;
        try
        {
            serializedMeta = JsonSerializer.Serialize(metaValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not serialize meta with {Key} and value {Value}", metaKey, metaValue);
            return;
        }

        await SetPersistentMeta(metaKey, serializedMeta, token);
    }

    public async Task<EFMeta> GetPersistentMeta(string metaKey, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(metaKey))
        {
            return null;
        }

        await using var context = _contextFactory.CreateContext(false);
        return await context.EFMeta
            .Where(meta => meta.ClientId == null)
            .FirstOrDefaultAsync(meta => meta.Key == metaKey, token);
    }

    public async Task<T> GetPersistentMetaValue<T>(string metaKey, CancellationToken token = default) where T : class
    {
        if (string.IsNullOrWhiteSpace(metaKey))
        {
            return null;
        }

        var meta = await GetPersistentMeta(metaKey, token);

        if (meta is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(meta.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not serialize meta with key {Key} and value {Value}", metaKey, meta.Value);
            return null;
        }
    }

    public async Task RemovePersistentMeta(string metaKey, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(metaKey))
        {
            return;
        }

        await using var context = _contextFactory.CreateContext(enableTracking: false);

        var existingMeta = await context.EFMeta
            .Where(meta => meta.Key == metaKey)
            .Where(meta => meta.ClientId == null)
            .FirstOrDefaultAsync(token);

        if (existingMeta != null)
        {
            _logger.LogDebug("Removing meta for key {Key} with id {Id}", metaKey, existingMeta.MetaId);
            context.Remove(existingMeta);
            await context.SaveChangesAsync(token);
        }
    }

    public void AddRuntimeMeta<T, TReturnType>(MetaType metaKey,
        Func<T, CancellationToken, Task<IEnumerable<TReturnType>>> metaAction, string entityPermission)
        where T : PaginationRequest where TReturnType : IClientMeta
    {
        var entity = Enum.Parse<WebfrontEntity>(entityPermission);
        if (!_metaActions.TryGetValue(metaKey, out var action))
        {
            _metaActions.Add(metaKey, [new MetaRegistration { Action = metaAction, EntityType = entity }]);
        }

        else
        {
            action.Add(new MetaRegistration { Action = metaAction, EntityType = entity });
        }
    }

    public void AddRuntimeMeta<T, TReturnType>(MetaType metaKey,
        Func<T, CancellationToken, Task<IEnumerable<TReturnType>>> metaAction)
        where T : PaginationRequest where TReturnType : IClientMeta
    {
        AddRuntimeMeta(metaKey, metaAction, nameof(WebfrontEntity.Default));
    }

    public async Task<IEnumerable<IClientMeta>> GetRuntimeMeta(ClientPaginationRequest request,
        CancellationToken token = default)
    {
        var metas = await Task.WhenAll(_metaActions.Where(kvp => kvp.Key != MetaType.Information)
            .SelectMany(kvp => kvp.Value)
            .Where(reg => reg.EntityType == WebfrontEntity.Default || _configuration.HasPermission(
                request.RequestPermission,
                reg.EntityType, WebfrontPermission.Read))
            .Select(async reg => await reg.Action(request, token)));

        return metas.SelectMany(m => (IEnumerable<IClientMeta>)m)
            .OrderByDescending(m => m.When)
            .Take(request.Count)
            .ToList();
    }

    public async Task<IEnumerable<T>> GetRuntimeMeta<T>(ClientPaginationRequest request, MetaType metaType,  CancellationToken token = default)
        where T : IClientMeta
    {
        if (metaType == MetaType.Information)
        {
            var allMeta = new List<T>();

            var completedMeta = await Task.WhenAll(_metaActions[metaType].Select(async individualMetaRegistration =>
                (IEnumerable<T>)await individualMetaRegistration.Action(request, token)));

            allMeta.AddRange(completedMeta.SelectMany(meta => meta));

            return ProcessInformationMeta(allMeta);
        }

        var registration = _metaActions[metaType][0];

        if (registration.EntityType != WebfrontEntity.Default && !_configuration.HasPermission(request.RequestPermission,
                registration.EntityType, WebfrontPermission.Read))
        {
            return [];
        }

        var meta = await registration.Action(request, token) as IEnumerable<T>;

        return meta;
    }

    private static IEnumerable<T> ProcessInformationMeta<T>(IEnumerable<T> meta) where T : IClientMeta
    {
        return meta;
    }

    private static bool ValidArgs(string key, int clientId) => !string.IsNullOrWhiteSpace(key) && clientId > 0;
}
