using static SharedLibraryCore.Database.Models.EFPenalty;

namespace WebfrontCore.ViewModels
{
    /// <summary>
    /// helper class to determine the filters to apply to penalties
    /// </summary>
    public class PenaltyFilterInfo
    {
        /// <summary>
        /// number of items offset from the start of the list
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// show only a certain type of penalty
        /// </summary>
        public PenaltyType ShowOnly { get; set; }

        /// <summary>
        /// ignore penalties that are automated
        /// </summary>
        public bool IgnoreAutomated { get; set; }
    }
}
