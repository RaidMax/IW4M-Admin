using SharedLibraryCore.Dtos;

namespace WebfrontCore.Controllers.API.Models;

public class TopStatsRequest : PaginationRequest
{
    public string? ServerId { get; set; }
}
