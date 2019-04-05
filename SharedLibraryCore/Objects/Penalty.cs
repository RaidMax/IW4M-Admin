using System;
using SharedLibraryCore;

namespace SharedLibraryCore.Objects
{
    public class Penalty : Database.Models.EFPenalty
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
