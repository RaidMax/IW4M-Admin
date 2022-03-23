namespace SharedLibraryCore.Interfaces;

public interface ILookupValue<TObject>
{
    int Id { get; }
    TObject Value { get; }
}
