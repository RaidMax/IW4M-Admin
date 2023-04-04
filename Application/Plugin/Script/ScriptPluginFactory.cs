using System;
using IW4MAdmin.Application.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Plugin.Script;

public class ScriptPluginFactory : IScriptPluginFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ScriptPluginFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public object CreateScriptPlugin(Type type, string fileName)
    {
        if (type == typeof(IPlugin))
        {
            return new ScriptPlugin(_serviceProvider.GetRequiredService<ILogger<ScriptPlugin>>(),
                fileName);
        }

        return new ScriptPluginV2(fileName, _serviceProvider.GetRequiredService<ILogger<ScriptPluginV2>>(),
            _serviceProvider.GetRequiredService<IScriptPluginServiceResolver>(),
            _serviceProvider.GetRequiredService<IScriptCommandFactory>(),
            _serviceProvider.GetRequiredService<IConfigurationHandlerV2<ScriptPluginConfiguration>>(),
            _serviceProvider.GetRequiredService<IInteractionRegistration>());
    }
}
