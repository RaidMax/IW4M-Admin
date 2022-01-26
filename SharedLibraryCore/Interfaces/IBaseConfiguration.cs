namespace SharedLibraryCore.Interfaces
{
    public interface IBaseConfiguration
    {
        string Name();
        IBaseConfiguration Generate();
    }
}