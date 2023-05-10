using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models;
using Data.Models.Client.Stats;
using Data.Models.Client.Stats.Reference;
using Data.Models.Server;
using IW4MAdmin.Plugins.Stats.Client.Abstractions;
using IW4MAdmin.Plugins.Stats.Client.Game;
using IW4MAdmin.Plugins.Stats.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Events;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Management;
using Stats.Client.Abstractions;
using Stats.Client.Game;

namespace IW4MAdmin.Plugins.Stats.Client;

public class HitState
{
    public HitState()
    {
        OnTransaction = new SemaphoreSlim(1, 1);
    }

    ~HitState()
    {
        OnTransaction.Dispose();
    }

    public List<EFClientHitStatistic> Hits { get; set; }
    public DateTime? LastUsage { get; set; }
    public int? LastWeaponId { get; set; }
    public EFServer Server { get; set; }
    public SemaphoreSlim OnTransaction { get; }
    public int UpdateCount { get; set; }
}

public class HitCalculator : IClientStatisticCalculator
{
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly ILogger<HitCalculator> _logger;

    private readonly ConcurrentDictionary<int, HitState> _clientHitStatistics = new();

    private readonly SemaphoreSlim _onTransaction = new SemaphoreSlim(1, 1);

    private readonly ILookupCache<EFServer> _serverCache;
    private readonly ILookupCache<EFHitLocation> _hitLocationCache;
    private readonly ILookupCache<EFWeapon> _weaponCache;
    private readonly ILookupCache<EFWeaponAttachment> _attachmentCache;
    private readonly ILookupCache<EFWeaponAttachmentCombo> _attachmentComboCache;
    private readonly ILookupCache<EFMeansOfDeath> _modCache;
    private readonly IHitInfoBuilder _hitInfoBuilder;
    private readonly IServerDistributionCalculator _serverDistributionCalculator;

    private readonly TimeSpan _maxActiveTime = TimeSpan.FromMinutes(2);
    private const int MaxUpdatesBeforePersist = 20;
    private const string SessionScores = nameof(SessionScores);

    public HitCalculator(ILogger<HitCalculator> logger, IDatabaseContextFactory contextFactory,
        ILookupCache<EFHitLocation> hitLocationCache, ILookupCache<EFWeapon> weaponCache,
        ILookupCache<EFWeaponAttachment> attachmentCache,
        ILookupCache<EFWeaponAttachmentCombo> attachmentComboCache,
        ILookupCache<EFServer> serverCache, ILookupCache<EFMeansOfDeath> modCache, IHitInfoBuilder hitInfoBuilder,
        IServerDistributionCalculator serverDistributionCalculator)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _hitLocationCache = hitLocationCache;
        _weaponCache = weaponCache;
        _attachmentCache = attachmentCache;
        _attachmentComboCache = attachmentComboCache;
        _serverCache = serverCache;
        _hitInfoBuilder = hitInfoBuilder;
        _modCache = modCache;
        _serverDistributionCalculator = serverDistributionCalculator;
    }

    public async Task GatherDependencies()
    {
        await _hitLocationCache.InitializeAsync();
        await _weaponCache.InitializeAsync();
        await _attachmentCache.InitializeAsync();
        await _attachmentComboCache.InitializeAsync();
        await _serverCache.InitializeAsync();
        await _modCache.InitializeAsync();
    }

    public async Task CalculateForEvent(CoreEvent coreEvent)
    {
        if (coreEvent is ClientStateInitializeEvent clientStateInitializeEvent)
        {
            // if no servers have been cached yet we need to pull them here
            // as they could have gotten added after we've initialized
            if (!_serverCache.GetAll().Any())
            {
                await _serverCache.InitializeAsync();
            }

            clientStateInitializeEvent.Client.SetAdditionalProperty(SessionScores, new List<(int, DateTime)>());
            return;
        }

        if (coreEvent is ClientStateDisposeEvent clientStateDisposeEvent)
        {
            _clientHitStatistics.Remove(clientStateDisposeEvent.Client.ClientId, out var state);

            if (state == null)
            {
                _logger.LogWarning("No client hit state available for disconnecting client {Client}",
                    clientStateDisposeEvent.Client.ToString());
                return;
            }

            try
            {
                await state.OnTransaction.WaitAsync();
                HandleDisconnectCalculations(clientStateDisposeEvent.Client, state);
                await UpdateClientStatistics(clientStateDisposeEvent.Client.ClientId, state);
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not handle disconnect calculations for client {Client}",
                    clientStateDisposeEvent.Client.ToString());
            }

            finally
            {
                if (state.OnTransaction.CurrentCount == 0)
                {
                    state.OnTransaction.Release();
                }
            }

            return;
        }

        if (coreEvent is MatchEndEvent matchEndEvent)
        {
            foreach (var client in matchEndEvent.Server.ConnectedClients)
            {
                var scores = client.GetAdditionalProperty<List<(int, DateTime)>>(SessionScores);
                scores?.Add((client.GetAdditionalProperty<int?>(StatManager.ESTIMATED_SCORE) ?? client.Score,
                    DateTime.Now));
            }
        }

        var damageEvent = coreEvent as ClientKillEvent ?? coreEvent as ClientDamageEvent;
           
        if (damageEvent is null)
        {
            return;
        }

        var eventRegex = damageEvent is ClientKillEvent
            ? damageEvent.Owner.EventParser.Configuration.Kill
            : damageEvent.Owner.EventParser.Configuration.Damage;

        var match = eventRegex.PatternMatcher.Match(damageEvent.Data);

        if (!match.Success)
        {
            _logger.LogWarning("Log for event type {Type} does not match pattern {LogLine}", damageEvent.Type,
                damageEvent.Data);
            return;
        }

        var attackerHitInfo = _hitInfoBuilder.Build(match.Values.ToArray(), eventRegex, damageEvent.Attacker.ClientId,
            damageEvent.Attacker.ClientId == damageEvent.Victim.ClientId, false, damageEvent.Server.GameCode);
        var victimHitInfo = _hitInfoBuilder.Build(match.Values.ToArray(), eventRegex, damageEvent.Victim.ClientId,
            damageEvent.Attacker.ClientId == damageEvent.Victim.ClientId, true, damageEvent.Server.GameCode);

        foreach (var hitInfo in new[] {attackerHitInfo, victimHitInfo})
        {
            if (hitInfo.MeansOfDeath == null || hitInfo.Location == null || hitInfo.Weapon == null || hitInfo.EntityId == 0)
            {
                _logger.LogDebug("Skipping hit because it does not contain the required data");
                continue;
            }
                
            try
            {
                await _onTransaction.WaitAsync();
                if (!_clientHitStatistics.ContainsKey(hitInfo.EntityId))
                {
                    _logger.LogDebug("Starting to track hits for {Client}", hitInfo.EntityId);
                    var clientHits = await GetHitsForClient(hitInfo.EntityId);
                    _clientHitStatistics.TryAdd(hitInfo.EntityId, new HitState
                    {
                        Hits = clientHits,
                        Server = await _serverCache
                            .FirstAsync(server =>
                                server.EndPoint == damageEvent.Server.Id && server.HostName != null)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not retrieve previous hit data for client {Client}", hitInfo.EntityId);
                continue;
            }

            finally
            {
                if (_onTransaction.CurrentCount == 0)
                {
                    _onTransaction.Release();
                }
            }

            var state = _clientHitStatistics[hitInfo.EntityId];

            try
            {
                await _onTransaction.WaitAsync();
                var calculatedHits = await RunTasksForHitInfo(hitInfo, state.Server.ServerId);

                foreach (var clientHit in calculatedHits)
                {
                    RunCalculation(clientHit, hitInfo, state);
                }
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not update hit calculations for {Client}", hitInfo.EntityId);
            }

            finally
            {
                if (_onTransaction.CurrentCount == 0)
                {
                    _onTransaction.Release();
                }
            }
        }
    }

    private async Task<IEnumerable<EFClientHitStatistic>> RunTasksForHitInfo(HitInfo hitInfo, long? serverId)
    {
        var weapon = await GetOrAddWeapon(hitInfo.Weapon, hitInfo.Game);
        var attachments =
            await Task.WhenAll(hitInfo.Weapon.Attachments.Select(attachment =>
                GetOrAddAttachment(attachment, hitInfo.Game)));
        var attachmentCombo = await GetOrAddAttachmentCombo(attachments, hitInfo.Game);
        var matchingLocation = await GetOrAddHitLocation(hitInfo.Location, hitInfo.Game);
        var meansOfDeath = await GetOrAddMeansOfDeath(hitInfo.MeansOfDeath, hitInfo.Game);

        var baseTasks = new[]
        {
            // just the client
            GetOrAddClientHit(hitInfo.EntityId, null),
            // client and server
            GetOrAddClientHit(hitInfo.EntityId, serverId),
            // just the location
            GetOrAddClientHit(hitInfo.EntityId, null, matchingLocation.HitLocationId),
            // location and server
            GetOrAddClientHit(hitInfo.EntityId, serverId, matchingLocation.HitLocationId),
            // per weapon
            GetOrAddClientHit(hitInfo.EntityId, null, null, weapon.WeaponId),
            // per weapon and server
            GetOrAddClientHit(hitInfo.EntityId, serverId, null, weapon.WeaponId),
            // means of death aggregate
            GetOrAddClientHit(hitInfo.EntityId, meansOfDeathId: meansOfDeath.MeansOfDeathId),
            // means of death per server aggregate
            GetOrAddClientHit(hitInfo.EntityId, serverId,
                meansOfDeathId: meansOfDeath.MeansOfDeathId)
        };

        var allTasks = baseTasks.AsEnumerable();

        if (attachmentCombo != null)
        {
            allTasks = allTasks
                // per weapon per attachment combo
                .Append(GetOrAddClientHit(hitInfo.EntityId, null, null,
                    weapon.WeaponId, attachmentCombo.WeaponAttachmentComboId))
                .Append(GetOrAddClientHit(hitInfo.EntityId, serverId, null,
                    weapon.WeaponId, attachmentCombo.WeaponAttachmentComboId));
        }

        return await Task.WhenAll(allTasks);
    }

    private void RunCalculation(EFClientHitStatistic clientHit, HitInfo hitInfo, HitState hitState)
    {
        if (hitInfo.HitType == HitType.Kill || hitInfo.HitType == HitType.Damage)
        {
            if (clientHit.WeaponId != null) // we only want to calculate usage time for weapons
            {
                var timeElapsed = DateTime.Now - hitState.LastUsage;
                var isSameWeapon = clientHit.WeaponId == hitState.LastWeaponId;

                clientHit.UsageSeconds ??= 60;

                if (timeElapsed.HasValue && timeElapsed <= _maxActiveTime)
                {
                    clientHit.UsageSeconds
                        += // if it's the same weapon we can count the entire elapsed time
                        // otherwise we split it to make a best guess
                        (int) Math.Round(timeElapsed.Value.TotalSeconds / (isSameWeapon ? 1.0 : 2.0));
                }

                hitState.LastUsage = DateTime.Now;
            }

            clientHit.DamageInflicted += hitInfo.Damage;
            clientHit.HitCount++;
        }

        if (hitInfo.HitType == HitType.Kill)
        {
            clientHit.KillCount++;
        }

        if (hitInfo.HitType == HitType.WasKilled || hitInfo.HitType == HitType.WasDamaged ||
            hitInfo.HitType == HitType.Suicide)
        {
            clientHit.ReceivedHitCount++;
            clientHit.DamageReceived += hitInfo.Damage;
        }

        if (hitInfo.HitType == HitType.WasKilled)
        {
            clientHit.DeathCount++;
        }
    }

    private async Task<List<EFClientHitStatistic>> GetHitsForClient(int clientId)
    {
        try
        {
            await using var context = _contextFactory.CreateContext();
            var hitLocations = await context.Set<EFClientHitStatistic>()
                .Where(stat => stat.ClientId == clientId)
                .ToListAsync();

            return !hitLocations.Any() ? new List<EFClientHitStatistic>() : hitLocations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not retrieve {hitName} for client with id {id}",
                nameof(EFClientHitStatistic), clientId);
        }

        return new List<EFClientHitStatistic>();
    }

    private async Task UpdateClientStatistics(int clientId, HitState locState = null)
    {
        if (!_clientHitStatistics.ContainsKey(clientId) && locState == null)
        {
            _logger.LogError("No {statsName} found for id {id}", nameof(EFClientHitStatistic), clientId);
            return;
        }

        var state = locState ?? _clientHitStatistics[clientId];

        try
        {
            await using var context = _contextFactory.CreateContext();
            context.Set<EFClientHitStatistic>().UpdateRange(state.Hits);
            await context.SaveChangesAsync();
        }

        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not update hit location stats for id {id}", clientId);
        }
    }

    private async Task<EFClientHitStatistic> GetOrAddClientHit(int clientId, long? serverId = null,
        int? hitLocationId = null, int? weaponId = null, int? attachmentComboId = null,
        int? meansOfDeathId = null)
    {
        var state = _clientHitStatistics[clientId];
        await state.OnTransaction.WaitAsync();

        var hitStat = state.Hits
            .FirstOrDefault(hit => hit.HitLocationId == hitLocationId
                                   && hit.WeaponId == weaponId
                                   && hit.WeaponAttachmentComboId == attachmentComboId
                                   && hit.MeansOfDeathId == meansOfDeathId
                                   && hit.ServerId == serverId);

        if (hitStat != null)
        {
            state.OnTransaction.Release();
            return hitStat;
        }

        hitStat = new EFClientHitStatistic()
        {
            ClientId = clientId,
            ServerId = serverId,
            WeaponId = weaponId,
            WeaponAttachmentComboId = attachmentComboId,
            HitLocationId = hitLocationId,
            MeansOfDeathId = meansOfDeathId
        };

        try
        {
            /*if (state.UpdateCount > MaxUpdatesBeforePersist)
            {
                await UpdateClientStatistics(clientId);
                state.UpdateCount = 0;
            }

            state.UpdateCount++;*/
            state.Hits.Add(hitStat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not add {statsName} for {id}", nameof(EFClientHitStatistic),
                clientId);
            state.Hits.Remove(hitStat);
        }
        finally
        {
            if (state.OnTransaction.CurrentCount == 0)
            {
                state.OnTransaction.Release();
            }
        }

        return hitStat;
    }

    private async Task<EFHitLocation> GetOrAddHitLocation(string location, Reference.Game game)
    {
        var matchingLocation = (await _hitLocationCache
            .FirstAsync(loc => loc.Name == location && loc.Game == game));

        if (matchingLocation != null)
        {
            return matchingLocation;
        }

        var hitLocation = new EFHitLocation()
        {
            Name = location,
            Game = game
        };

        hitLocation = await _hitLocationCache.AddAsync(hitLocation);

        return hitLocation;
    }

    private async Task<EFWeapon> GetOrAddWeapon(WeaponInfo weapon, Reference.Game game)
    {
        var matchingWeapon = (await _weaponCache
            .FirstAsync(wep => wep.Name == weapon.Name && wep.Game == game));

        if (matchingWeapon != null)
        {
            return matchingWeapon;
        }

        matchingWeapon = new EFWeapon()
        {
            Name = weapon.Name,
            Game = game
        };

        matchingWeapon = await _weaponCache.AddAsync(matchingWeapon);

        return matchingWeapon;
    }

    private async Task<EFWeaponAttachment> GetOrAddAttachment(AttachmentInfo attachment, Reference.Game game)
    {
        var matchingAttachment = (await _attachmentCache
            .FirstAsync(attach => attach.Name == attachment.Name && attach.Game == game));

        if (matchingAttachment != null)
        {
            return matchingAttachment;
        }

        matchingAttachment = new EFWeaponAttachment()
        {
            Name = attachment.Name,
            Game = game
        };

        matchingAttachment = await _attachmentCache.AddAsync(matchingAttachment);

        return matchingAttachment;
    }

    private async Task<EFWeaponAttachmentCombo> GetOrAddAttachmentCombo(EFWeaponAttachment[] attachments,
        Reference.Game game)
    {
        if (!attachments.Any())
        {
            return null;
        }

        var allAttachments = attachments.ToList();

        if (allAttachments.Count() < 3)
        {
            for (var i = allAttachments.Count(); i <= 3; i++)
            {
                allAttachments.Add(null);
            }
        }

        var matchingAttachmentCombo = (await _attachmentComboCache.FirstAsync(combo =>
            combo.Game == game
            && combo.Attachment1Id == allAttachments[0].Id
            && combo.Attachment2Id == allAttachments[1]?.Id
            && combo.Attachment3Id == allAttachments[2]?.Id));

        if (matchingAttachmentCombo != null)
        {
            return matchingAttachmentCombo;
        }

        matchingAttachmentCombo = new EFWeaponAttachmentCombo()
        {
            Game = game,
            Attachment1Id = (int) allAttachments[0].Id,
            Attachment2Id = (int?) allAttachments[1]?.Id,
            Attachment3Id = (int?) allAttachments[2]?.Id,
        };

        matchingAttachmentCombo = await _attachmentComboCache.AddAsync(matchingAttachmentCombo);

        return matchingAttachmentCombo;
    }

    private async Task<EFMeansOfDeath> GetOrAddMeansOfDeath(string meansOfDeath, Reference.Game game)
    {
        var matchingMod = (await _modCache
            .FirstAsync(mod => mod.Name == meansOfDeath && mod.Game == game));

        if (matchingMod != null)
        {
            return matchingMod;
        }

        var mod = new EFMeansOfDeath()
        {
            Name = meansOfDeath,
            Game = game
        };

        mod = await _modCache.AddAsync(mod);

        return mod;
    }

    private void HandleDisconnectCalculations(EFClient client, HitState state)
    {
        // todo: this not added to states fast connect/disconnect
        var serverStats = state.Hits.FirstOrDefault(stat =>
            stat.ServerId == state.Server.ServerId && stat.WeaponId == null &&
            stat.WeaponAttachmentComboId == null && stat.HitLocationId == null && stat.MeansOfDeathId == null);

        if (serverStats == null)
        {
            _logger.LogWarning("No server hits were found for {serverId} on disconnect for {client}",
                state.Server.ServerId, client.ToString());
            return;
        }

        var aggregate = state.Hits.FirstOrDefault(stat => stat.WeaponId == null &&
                                                          stat.WeaponAttachmentComboId == null &&
                                                          stat.HitLocationId == null &&
                                                          stat.MeansOfDeathId == null &&
                                                          stat.ServerId == null);

        if (aggregate == null)
        {
            _logger.LogWarning("No aggregate found for {serverId} on disconnect for {client}",
                state.Server.ServerId, client.ToString());
            return;
        }

        var sessionScores = client.GetAdditionalProperty<List<(int, DateTime)>>(SessionScores);

        if (sessionScores == null)
        {
            _logger.LogWarning("No session scores available for {Client}", client.ToString());
            return;
        }

        foreach (var stat in new[] {serverStats, aggregate})
        {
            stat.Score ??= 0;

            if (sessionScores.Count == 0)
            {
                stat.Score += client.Score > 0 ? client.Score : client.GetAdditionalProperty<int?>(Helpers.StatManager.ESTIMATED_SCORE) ?? 0 * 50;
            }

            else
            {
                stat.Score += sessionScores.Sum(item => item.Item1) +
                              (sessionScores.Last().Item1 == client.Score &&
                               (DateTime.Now - sessionScores.Last().Item2).TotalMinutes < 1
                                  ? 0
                                  : client.Score);
            }
        }
    }
}
