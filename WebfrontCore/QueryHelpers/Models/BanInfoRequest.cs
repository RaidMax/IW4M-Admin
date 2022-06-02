using SharedLibraryCore.Dtos;

namespace WebfrontCore.QueryHelpers.Models;

public class BanInfoRequest : PaginationRequest
{
    public string ClientName { get; set; }
}
