using System;
using System.Text.Json.Serialization;

namespace SharedLibraryCore.Dtos.Meta.Responses;

public class ClientNoteMetaResponse
{
    public string Note { get; set; }
    public int OriginEntityId { get; set; }
    [JsonIgnore]
    public string? OriginEntityName { get; set; }
    public DateTime ModifiedDate { get; set; }
}
