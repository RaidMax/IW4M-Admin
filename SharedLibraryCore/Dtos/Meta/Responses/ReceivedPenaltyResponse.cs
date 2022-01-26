using System;
using Data.Models;

namespace SharedLibraryCore.Dtos.Meta.Responses
{
    public class ReceivedPenaltyResponse : BaseMetaResponse
    {
        public int PenaltyId { get; set; }
        public int OffenderClientId { get; set; }
        public string OffenderName { get; set; }
        public string PunisherName { get; set; }
        public int PunisherClientId { get; set; }
        public EFPenalty.PenaltyType PenaltyType { get; set; }
        public string Offense { get; set; }
        public string AutomatedOffense { get; set; }
        public DateTime? ExpirationDate { get; set; }

        public string ExpiresInText => ExpirationDate.HasValue && ExpirationDate.Value > DateTime.UtcNow
            ? (ExpirationDate - DateTime.UtcNow).Value.HumanizeForCurrentCulture()
            : "";

        public string LengthText => ExpirationDate.HasValue
            ? (ExpirationDate.Value.AddMinutes(1) - When).HumanizeForCurrentCulture()
            : "";

        public bool IsLinked { get; set; }
        public int LinkedClientId { get; set; }
    }
}