using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Data.Models;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Dtos
{
    public class PlayerInfo
    {
        public string Name { get; set; }
        public Reference.Game Game { get; set; }
        public int ClientId { get; set; }
        public string Level { get; set; }
        public string Tag { get; set; }
        public int LevelInt { get; set; }
        public string IPAddress { get; set; }
        public long NetworkId { get; set; }
        public List<ProfileMetaEntry> Aliases { get; set; }
        public List<ProfileMetaEntry> IPs { get; set; }
        public bool HasActivePenalty { get; set; }
        public string ActivePenaltyType { get; set; }
        public bool Authenticated { get; set; }
        public List<InformationResponse> Meta { get; set; }
        public EFPenalty ActivePenalty { get; set; }
        public bool Online { get; set; }
        public string TimeOnline { get; set; }
        public DateTime LastConnection { get; set; }
        public string LastConnectionText => (DateTime.UtcNow - LastConnection).HumanizeForCurrentCulture();
        public IDictionary<int, long> LinkedAccounts { get; set; }
        public MetaType? MetaFilterType { get; set; }
        public double? ZScore { get; set; }
        public string ConnectProtocolUrl { get;set; }
        public string CurrentServerName { get; set; }
        public GeoLocationInfo GeoLocationInfo { get; set; }
        public ClientNoteMetaResponse NoteMeta { get; set; }
        
        public List<InteractionInfo> Interactions { get; set; }
    }
}
