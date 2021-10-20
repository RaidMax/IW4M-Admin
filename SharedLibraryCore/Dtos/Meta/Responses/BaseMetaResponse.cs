using SharedLibraryCore.Interfaces;
using System;

namespace SharedLibraryCore.Dtos.Meta.Responses
{
    public class BaseMetaResponse : IClientMeta, IClientMetaResponse
    {
        public long MetaId { get; set; }
        public int ClientId { get; set; }
        public MetaType Type { get; set; }
        public DateTime When { get; set; }
        public bool IsSensitive { get; set; }
        public bool ShouldDisplay { get; set; }
        public int? Column { get; set; }
        public int? Order { get; set; }
    }
}
