using static SharedLibraryCore.Database.Models.EFPenalty;

namespace WebfrontCore.ViewModels
{
    public class PenaltyFilterInfo
    {
        public int Offset { get; set; }
        public PenaltyType ShowOnly { get; set; }
    }
}
