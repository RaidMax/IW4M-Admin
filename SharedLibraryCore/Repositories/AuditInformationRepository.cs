using Data.Abstractions;
using Data.Models;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Repositories;

/// <summary>
///     implementation of IAuditInformationRepository
/// </summary>
public class AuditInformationRepository(IDatabaseContextFactory contextFactory) : IAuditInformationRepository
{
    /// <inheritdoc />
    public async Task<IList<AuditInfo>> ListAuditInformation(AuditFilterRequest request)
    {
        await using var ctx = contextFactory.CreateContext(false);
            
        var query = BuildBaseQuery(ctx, request);

        var iqItems = query
            .OrderByDescending(x => x.TimeChanged)
            .Skip(request.Offset)
            .Take(request.Count)
            .Select(x => new AuditInfo
            {
                Action = x.TypeOfChange.ToString(),
                OriginName = ctx.Clients
                    .Where(c => c.ClientId == (x.ImpersonationEntityId ?? x.OriginEntityId))
                    .Select(c => c.CurrentAlias.Name)
                    .FirstOrDefault() ?? "",
                OriginId = x.ImpersonationEntityId ?? x.OriginEntityId,
                OriginIPAddress = ctx.Clients
                    .Where(c => c.ClientId == (x.ImpersonationEntityId ?? x.OriginEntityId))
                    .Select(c => c.CurrentAlias.SearchableIPAddress)
                    .FirstOrDefault() ?? "",
                TargetName = ctx.Clients
                    .Where(c => c.ClientId == x.TargetEntityId)
                    .Select(c => c.CurrentAlias.Name)
                    .FirstOrDefault() ?? "",
                TargetId = x.TargetEntityId == 0 ? null : x.TargetEntityId,
                When = x.TimeChanged,
                Data = x.Comment,
                OldValue = x.PreviousValue,
                NewValue = x.CurrentValue
            });

        return await iqItems.ToListAsync();
    }

    /// <inheritdoc />
    public async Task<AuditStatistics> GetStatisticsAsync(AuditFilterRequest request)
    {
        await using var ctx = contextFactory.CreateContext(false);
        var query = BuildBaseQuery(ctx, request);

        var totalCount = await query.CountAsync();

        var countByType = await query
            .GroupBy(x => x.TypeOfChange)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count);

        var topAdminIds = await query
            .GroupBy(x => x.ImpersonationEntityId ?? x.OriginEntityId)
            .Select(g => new { ClientId = g.Key, ActionCount = g.Count() })
            .OrderByDescending(x => x.ActionCount)
            .Take(5)
            .ToListAsync();

        var topAdmins = new List<AdminActivityInfo>();
        foreach (var admin in topAdminIds)
        {
            var name = await ctx.Clients
                .Where(c => c.ClientId == admin.ClientId)
                .Select(c => c.CurrentAlias.Name)
                .FirstOrDefaultAsync() ?? "Unknown";
                
            topAdmins.Add(new AdminActivityInfo
            {
                ClientId = admin.ClientId,
                Name = name,
                ActionCount = admin.ActionCount
            });
        }

        return new AuditStatistics
        {
            TotalCount = totalCount,
            CountByActionType = countByType,
            MostActiveAdmins = topAdmins
        };
    }

    private static IQueryable<EFChangeHistory> BuildBaseQuery(Data.Context.DatabaseContext ctx, AuditFilterRequest request)
    {
        var query = ctx.EFChangeHistory
            .Where(change => change.TypeOfChange != EFChangeHistory.ChangeType.Ban);

        // Filter by action types
        if (request.ActionTypes is { Count: > 0 })
        {
            query = query.Where(x => request.ActionTypes.Contains(x.TypeOfChange));
        }

        // Filter by origin (admin) ID
        if (request.OriginId.HasValue)
        {
            query = query.Where(x => 
                x.OriginEntityId == request.OriginId.Value || 
                x.ImpersonationEntityId == request.OriginId.Value);
        }

        // Filter by target ID
        if (request.TargetId.HasValue)
        {
            query = query.Where(x => x.TargetEntityId == request.TargetId.Value);
        }

        // Filter by date range
        if (request.After.HasValue)
        {
            query = query.Where(x => x.TimeChanged >= request.After.Value);
        }

        if (request.Before.HasValue)
        {
            query = query.Where(x => x.TimeChanged <= request.Before.Value);
        }

        // Text search in comment
        if (!string.IsNullOrWhiteSpace(request.SearchQuery))
        {
            var searchTerm = request.SearchQuery.ToLower();
            query = query.Where(x => 
                x.Comment != null && x.Comment.ToLower().Contains(searchTerm));
        }

        return query;
    }
}
