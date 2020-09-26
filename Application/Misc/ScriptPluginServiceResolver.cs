using SharedLibraryCore.Interfaces;
using System;
using System.Linq;

namespace IW4MAdmin.Application.Misc
{
    /// <summary>
    /// implementation of IScriptPluginServiceResolver
    /// </summary>
    public class ScriptPluginServiceResolver : IScriptPluginServiceResolver
    {
        private readonly IServiceProvider _serviceProvider;

        public ScriptPluginServiceResolver(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public object ResolveService(string serviceName)
        {
            var serviceType = typeof(IScriptPluginServiceResolver).Assembly.GetTypes().FirstOrDefault(_type => _type.Name == serviceName);

            if (serviceType == null)
            {
                throw new InvalidOperationException($"No service type '{serviceName}' defined in IW4MAdmin assembly");
            }

            return _serviceProvider.GetService(serviceType);
        }
    }
}
