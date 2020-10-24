using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using SharedLibraryCore.Interfaces;
using System.Linq;
using SharedLibraryCore;
using IW4MAdmin.Application.Misc;
using IW4MAdmin.Application.API.Master;
using SharedLibraryCore.Configuration;

namespace IW4MAdmin.Application.Helpers
{
    /// <summary>
    /// implementation of IPluginImporter
    /// discovers plugins and script plugins
    /// </summary>
    public class PluginImporter : IPluginImporter
    {
        private IEnumerable<PluginSubscriptionContent> _pluginSubscription;
        private static readonly string PLUGIN_DIR = "Plugins";
        private readonly ILogger _logger;
        private readonly IRemoteAssemblyHandler _remoteAssemblyHandler;
        private readonly IMasterApi _masterApi;
        private readonly ApplicationConfiguration _appConfig;

        public PluginImporter(ILogger logger, ApplicationConfiguration appConfig, IMasterApi masterApi, IRemoteAssemblyHandler remoteAssemblyHandler)
        {
            _logger = logger;
            _masterApi = masterApi;
            _remoteAssemblyHandler = remoteAssemblyHandler;
            _appConfig = appConfig;
        }

        /// <summary>
        /// discovers all the script plugins in the plugins dir
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IPlugin> DiscoverScriptPlugins()
        {
            string pluginDir = $"{Utilities.OperatingDirectory}{PLUGIN_DIR}{Path.DirectorySeparatorChar}";

            if (Directory.Exists(pluginDir))
            {
                var scriptPluginFiles = Directory.GetFiles(pluginDir, "*.js").AsEnumerable().Union(GetRemoteScripts());

                _logger.WriteInfo($"Discovered {scriptPluginFiles.Count()} potential script plugins");

                if (scriptPluginFiles.Count() > 0)
                {
                    foreach (string fileName in scriptPluginFiles)
                    {
                        _logger.WriteInfo($"Discovered script plugin {fileName}");
                        var plugin = new ScriptPlugin(fileName);
                        yield return plugin;
                    }
                }
            }
        }

        /// <summary>
        /// discovers all the C# assembly plugins and commands
        /// </summary>
        /// <returns></returns>
        public (IEnumerable<Type>, IEnumerable<Type>) DiscoverAssemblyPluginImplementations()
        {
            string pluginDir = $"{Utilities.OperatingDirectory}{PLUGIN_DIR}{Path.DirectorySeparatorChar}";
            var pluginTypes = Enumerable.Empty<Type>();
            var commandTypes = Enumerable.Empty<Type>();

            if (Directory.Exists(pluginDir))
            {
                var dllFileNames = Directory.GetFiles(pluginDir, "*.dll");
                _logger.WriteInfo($"Discovered {dllFileNames.Length} potential plugin assemblies");

                if (dllFileNames.Length > 0)
                {
                    // we only want to load the most recent assembly in case of duplicates
                    var assemblies = dllFileNames.Select(_name => Assembly.LoadFrom(_name))
                        .Union(GetRemoteAssemblies())
                        .GroupBy(_assembly => _assembly.FullName).Select(_assembly => _assembly.OrderByDescending(_assembly => _assembly.GetName().Version).First());

                    pluginTypes = assemblies
                        .SelectMany(_asm => _asm.GetTypes())
                        .Where(_assemblyType => _assemblyType.GetInterface(nameof(IPlugin), false) != null);

                    _logger.WriteInfo($"Discovered {pluginTypes.Count()} plugin implementations");

                    commandTypes = assemblies
                        .SelectMany(_asm => _asm.GetTypes())
                        .Where(_assemblyType => _assemblyType.IsClass && _assemblyType.BaseType == typeof(Command));

                    _logger.WriteInfo($"Discovered {commandTypes.Count()} plugin commands");
                }
            }

            return (pluginTypes, commandTypes);
        }

        private IEnumerable<Assembly> GetRemoteAssemblies()
        {
            try
            {
                if (_pluginSubscription == null)
                    _pluginSubscription = _masterApi.GetPluginSubscription(Guid.Parse(_appConfig.Id), _appConfig.SubscriptionId).Result;

                return _remoteAssemblyHandler.DecryptAssemblies(_pluginSubscription.Where(sub => sub.Type == PluginType.Binary).Select(sub => sub.Content).ToArray());
            }

            catch (Exception ex)
            {
                _logger.WriteWarning("Could not load remote assemblies");
                _logger.WriteDebug(ex.GetExceptionInfo());
                return Enumerable.Empty<Assembly>();
            }
        }

        private IEnumerable<string> GetRemoteScripts()
        {
            try
            {
                if (_pluginSubscription == null)
                    _pluginSubscription = _masterApi.GetPluginSubscription(Guid.Parse(_appConfig.Id), _appConfig.SubscriptionId).Result;

                return _remoteAssemblyHandler.DecryptScripts(_pluginSubscription.Where(sub => sub.Type == PluginType.Script).Select(sub => sub.Content).ToArray());
            }

            catch (Exception ex)
            {
                _logger.WriteWarning("Could not load remote assemblies");
                _logger.WriteDebug(ex.GetExceptionInfo());
                return Enumerable.Empty<string>();
            }
        }
    }

    public enum PluginType
    {
        Binary,
        Script
    }
}
