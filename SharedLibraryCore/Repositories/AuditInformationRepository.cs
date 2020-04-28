using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SharedLibraryCore.Repositories
{
    /// <summary>
    /// implementation if IAuditInformationRepository
    /// </summary>
    public class AuditInformationRepository : IAuditInformationRepository
    {
        private readonly IDatabaseContextFactory _contextFactory;

        public AuditInformationRepository(IDatabaseContextFactory contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <inheritdoc/>
        public async Task<IList<AuditInfo>> ListAuditInformation(PaginationInfo paginationInfo)
        {
            using (var ctx = _contextFactory.CreateContext(enableTracking: false))
            {
                var iqItems = (from change in ctx.EFChangeHistory
                               where change.TypeOfChange != Database.Models.EFChangeHistory.ChangeType.Ban
                               orderby change.TimeChanged descending
                               join originClient in ctx.Clients
                               on (change.ImpersonationEntityId ?? change.OriginEntityId) equals originClient.ClientId
                               join targetClient in ctx.Clients
                               on change.TargetEntityId equals targetClient.ClientId
                               into targetChange
                               from targetClient in targetChange.DefaultIfEmpty()
                               select new AuditInfo()
                               {
                                   Action = change.TypeOfChange.ToString(),
                                   OriginName = originClient.CurrentAlias.Name,
                                   OriginId = originClient.ClientId,
                                   TargetName = targetClient == null ? "" : targetClient.CurrentAlias.Name,
                                   TargetId = targetClient == null ? new int?() : targetClient.ClientId,
                                   When = change.TimeChanged,
                                   Data = change.Comment,
                                   OldValue = change.PreviousValue,
                                   NewValue = change.CurrentValue
                               })
                    .Skip(paginationInfo.Offset)
                    .Take(paginationInfo.Count);

                return await iqItems.ToListAsync();
            }
        }
    }
}
