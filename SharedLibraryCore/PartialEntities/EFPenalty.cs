using System;
using SharedLibraryCore;

namespace SharedLibraryCore.Database.Models
{
    public partial class EFPenalty
    {
        public enum PenaltyType
        {
            Report,
            Warning,
            Flag,
            Kick,
            TempBan,
            Ban,
            Unban,
            Any,
            Unflag
        }
    }
}
