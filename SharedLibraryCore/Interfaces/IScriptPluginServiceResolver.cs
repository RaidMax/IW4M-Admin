namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    /// interface used to dynamically resolve services by string name
    /// </summary>
    public interface IScriptPluginServiceResolver
    {
        object ResolveService(string serviceName);
    }
}
