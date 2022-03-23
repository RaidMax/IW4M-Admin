using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Dtos;

public class LookupValue<TObject> : ILookupValue<TObject>
{
    public int Id { get; set; }
    public TObject Value { get; set; }
}
