using SharedLibraryCore.Dtos;

namespace SharedLibraryCore.QueryHelper
{
    public class ClientPaginationRequest : PaginationRequest
    {
        public int ClientId { get; set; }
        public bool IsPrivileged { get; set; }
    }
}
