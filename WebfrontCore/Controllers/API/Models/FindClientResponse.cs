using SharedLibraryCore.Dtos;

namespace WebfrontCore.Controllers.API.Models
{
    public class FindClientResponse
    {
        /// <summary>
        /// total number of client found matching the query
        /// </summary>
        public long TotalFoundClients { get; set; }

        /// <summary>
        /// collection of doun clients
        /// </summary>
        public IEnumerable<FindClientResult> Clients { get; set; }
    }
}
