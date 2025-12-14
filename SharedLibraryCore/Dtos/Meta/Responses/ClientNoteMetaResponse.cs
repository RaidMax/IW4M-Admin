using System;
using System.Text.Json.Serialization;

namespace SharedLibraryCore.Dtos.Meta.Responses;

public class ClientNoteMetaResponse : BaseMetaResponse
{
    public string Note { get; set; }
    public int OriginEntityId { get; set; }
    public string OriginEntityName { get; set; }
    public DateTime ModifiedDate { get; set; }
}
