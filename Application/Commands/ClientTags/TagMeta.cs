using System.Text.Json.Serialization;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Commands.ClientTags;

public class TagMeta : ILookupValue<string>
{
    [JsonIgnore] public int TagId => Id;
    [JsonIgnore] public string TagName => Value;

    public int Id { get; set; }
    public string Value { get; set; }
}
