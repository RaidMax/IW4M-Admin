using SharedLibraryCore.Dtos;

namespace WebfrontCore.QueryHelpers.Models;

public class BanInfoRequest : PaginationRequest
{
    public string ClientName { get; set; }
    public string ClientGuid { get; set; }
    public int? ClientId { get; set; }
    public string ClientIP { get; set; }
}
