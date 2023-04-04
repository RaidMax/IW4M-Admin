using Microsoft.Extensions.DependencyInjection;

namespace SharedLibraryCore.Interfaces;


public interface IPluginV2 : IModularAssembly
{
    static void RegisterDependencies(IServiceCollection serviceProvider)
    {
    }
}
