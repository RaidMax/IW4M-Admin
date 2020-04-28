using SharedLibraryCore.Dtos;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    /// describes the capabilities of the audit info repository
    /// </summary>
    public interface IAuditInformationRepository
    {
        /// <summary>
        /// retrieves a list of audit information for given pagination params
        /// </summary>
        /// <param name="paginationInfo">pagination info</param>
        /// <returns></returns>
        Task<IList<AuditInfo>> ListAuditInformation(PaginationInfo paginationInfo);
    }
}
