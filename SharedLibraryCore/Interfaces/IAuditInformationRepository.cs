using System.Collections.Generic;
using System.Threading.Tasks;
using SharedLibraryCore.Dtos;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    ///     describes the capabilities of the audit info repository
    /// </summary>
    public interface IAuditInformationRepository
    {
        /// <summary>
        ///     retrieves a list of audit information for given filter and pagination params
        /// </summary>
        /// <param name="request">filter and pagination request</param>
        /// <returns></returns>
        Task<IList<AuditInfo>> ListAuditInformation(AuditFilterRequest request);

        /// <summary>
        ///     retrieves statistics for the audit log dashboard
        /// </summary>
        /// <param name="request">filter request (pagination ignored)</param>
        /// <returns></returns>
        Task<AuditStatistics> GetStatisticsAsync(AuditFilterRequest request);
    }
}
