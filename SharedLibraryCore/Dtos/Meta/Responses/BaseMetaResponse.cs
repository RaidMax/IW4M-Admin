using System;
using SharedLibraryCore.Interfaces;

using System.Text.Json.Serialization;

namespace SharedLibraryCore.Dtos.Meta.Responses
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(InformationResponse), typeDiscriminator: "Information")]
    [JsonDerivedType(typeof(UpdatedAliasResponse), typeDiscriminator: "UpdatedAlias")]
    [JsonDerivedType(typeof(MessageResponse), typeDiscriminator: "Message")]
    [JsonDerivedType(typeof(ConnectionHistoryResponse), typeDiscriminator: "ConnectionHistory")]
    [JsonDerivedType(typeof(PermissionLevelChangedResponse), typeDiscriminator: "PermissionLevelChanged")]
    [JsonDerivedType(typeof(AdministeredPenaltyResponse), typeDiscriminator: "AdministeredPenalty")]
    [JsonDerivedType(typeof(ReceivedPenaltyResponse), typeDiscriminator: "ReceivedPenalty")]
    [JsonDerivedType(typeof(ClientNoteMetaResponse), typeDiscriminator: "ClientNote")]
    public class BaseMetaResponse : IClientMeta, IClientMetaResponse
    {
        public MetaType Type { get; set; }
        public DateTime When { get; set; }
        public bool IsSensitive { get; set; }
        public bool ShouldDisplay { get; set; }
        public int? Column { get; set; }
        public int? Order { get; set; }
        public long MetaId { get; set; }
        public int ClientId { get; set; }
    }
}