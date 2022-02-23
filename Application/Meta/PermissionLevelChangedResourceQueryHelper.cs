using System.Linq;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;

namespace IW4MAdmin.Application.Meta;

public class
    PermissionLevelChangedResourceQueryHelper : IResourceQueryHelper<ClientPaginationRequest,
        PermissionLevelChangedResponse>
{
    private readonly IDatabaseContextFactory _contextFactory;

    public PermissionLevelChangedResourceQueryHelper(IDatabaseContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<ResourceQueryHelperResult<PermissionLevelChangedResponse>> QueryResource(
        ClientPaginationRequest query)
    {
        await using var context = _contextFactory.CreateContext();

        var auditEntries = context.EFChangeHistory.Where(change => change.TargetEntityId == query.ClientId)
            .Where(change => change.TypeOfChange == EFChangeHistory.ChangeType.Permission);

        var audits = from change in auditEntries
            join client in context.Clients
                on change.OriginEntityId equals client.ClientId
            select new PermissionLevelChangedResponse
            {
                ChangedById = change.OriginEntityId,
                ChangedByName = client.CurrentAlias.Name,
                PreviousPermissionLevelValue = change.PreviousValue,
                CurrentPermissionLevelValue = change.CurrentValue,
                When = change.TimeChanged,
                ClientId = change.TargetEntityId
            };

        return new ResourceQueryHelperResult<PermissionLevelChangedResponse>
        {
            Results = await audits.Skip(query.Offset).Take(query.Count).ToListAsync()
        };
    }
}
