namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    ///     interface used to dynamically resolve services by string name
    /// </summary>
    public interface IScriptPluginServiceResolver
    {
        /// <summary>
        ///     resolves a service with the given name
        /// </summary>
        /// <param name="serviceName">class name of service</param>
        /// <returns></returns>
        object ResolveService(string serviceName);

        /// <summary>
        ///     resolves a service with the given name and generic params
        /// </summary>
        /// <param name="serviceName">class name of service</param>
        /// <param name="genericParameters">generic class names</param>
        /// <returns></returns>
        object ResolveService(string serviceName, string[] genericParameters);
    }
}
