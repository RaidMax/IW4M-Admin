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
            var serviceType = DetermineRootType(serviceName);
            return _serviceProvider.GetService(serviceType);
        }

        public object ResolveService(string serviceName, string[] genericParameters)
        {
            var serviceType = DetermineRootType(serviceName, genericParameters.Length);
            var genericTypes = genericParameters.Select(_genericTypeParam => DetermineRootType(_genericTypeParam));
            var resolvedServiceType = serviceType.MakeGenericType(genericTypes.ToArray());
            return _serviceProvider.GetService(resolvedServiceType);
        }

        private Type DetermineRootType(string serviceName, int genericParamCount = 0)
        {
            var typeCollection = AppDomain.CurrentDomain.GetAssemblies()
                       .SelectMany(t => t.GetTypes());
            string generatedName = $"{serviceName}{(genericParamCount == 0 ? "" : $"`{genericParamCount}")}".ToLower();
            var serviceType = typeCollection.FirstOrDefault(_type => _type.Name.ToLower() == generatedName);

            if (serviceType == null)
            {
                throw new InvalidOperationException($"No object type '{serviceName}' defined in loaded assemblies");
            }

            return serviceType;
        }
    }
}
