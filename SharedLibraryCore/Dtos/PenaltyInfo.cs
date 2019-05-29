using System;
using static SharedLibraryCore.Database.Models.EFClient;
using static SharedLibraryCore.Database.Models.EFPenalty;

namespace SharedLibraryCore.Dtos
{
    public class PenaltyInfo : SharedInfo
    {
        public string OffenderName { get; set; }
        public int OffenderId { get; set; }
        public ulong OffenderNetworkId { get; set; }
        public string OffenderIPAddress { get; set; }
        public string PunisherName { get; set; }
        public int PunisherId { get; set; }
        public ulong PunisherNetworkId { get; set; }
        public string PunisherIPAddress { get; set; }
        public Permission PunisherLevel { get; set; }
        public string PunisherLevelText => PunisherLevel.ToLocalizedLevelName();
        public string Offense { get; set; }
        public string AutomatedOffense { get; set; }
        public PenaltyType PenaltyType { get; set; }
        public string PenaltyTypeText => PenaltyType.ToString();
        public DateTime TimePunished { get; set; }
        public string TimePunishedString => Utilities.GetTimePassed(TimePunished, true);
        public string TimeRemaining => DateTime.UtcNow > Expires ? "" : $"{((Expires ?? DateTime.MaxValue).Year == DateTime.MaxValue.Year ? Utilities.GetTimePassed(TimePunished, true) : Utilities.TimeSpanText((Expires ?? DateTime.MaxValue) - DateTime.UtcNow))}";
        public bool Expired => Expires.HasValue && Expires <= DateTime.UtcNow;
        public DateTime? Expires { get; set; }
        public override bool Sensitive => PenaltyType == PenaltyType.Flag || PenaltyType == PenaltyType.Unflag;
        public bool IsEvade { get; set; }
        public string AdditionalPenaltyInformation => $"{(!string.IsNullOrEmpty(AutomatedOffense) ? $" ({AutomatedOffense})" : "")}{(IsEvade ? $" ({Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_PENALTY_EVADE"]})" : "")}";
    }
}
