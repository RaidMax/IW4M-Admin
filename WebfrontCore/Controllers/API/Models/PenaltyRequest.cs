using Data.Models;
using SharedLibraryCore.Dtos;

namespace WebfrontCore.Controllers.API.Models;

public class PenaltyRequest : PaginationRequest
{
    public EFPenalty.PenaltyType ShowOnly { get; set; } = EFPenalty.PenaltyType.Any;
    public bool IgnoreAutomated { get; set; } = true;
}
