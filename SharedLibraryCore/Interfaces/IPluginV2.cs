using System.Diagnostics.CodeAnalysis;

namespace SharedLibraryCore.Interfaces;

public interface IPluginV2 : IModularAssembly, IDisposable
{
    static void RegisterDependencies(IServiceCollection serviceCollection)
    {
    }

    [SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize",
        Justification = "Disabled warning because we need to let the plugins handle their dispose logic")]
    void IDisposable.Dispose()
    {
    }
}
