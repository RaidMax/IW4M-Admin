using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using IW4MAdmin.Application.API.Master;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Plugin
{
    /// <summary>
    /// implementation of IPluginImporter
    /// discovers plugins and script plugins
    /// </summary>
    public class PluginImporter : IPluginImporter
    {
        private IEnumerable<PluginSubscriptionContent> _pluginSubscription;
        private const string PluginDir = "Plugins";
        private const string PluginV2Match = "^ *((?:var|const|let) +init)|function init";
        private readonly ILogger _logger;
        private readonly IRemoteAssemblyHandler _remoteAssemblyHandler;
        private readonly IMasterApi _masterApi;
        private readonly ApplicationConfiguration _appConfig;

        private static readonly Type[] FilterTypes =
        {
            typeof(IPlugin),
            typeof(IPluginV2),
            typeof(Command),
            typeof(IBaseConfiguration)
        };

        public PluginImporter(ILogger<PluginImporter> logger, ApplicationConfiguration appConfig, IMasterApi masterApi,
            IRemoteAssemblyHandler remoteAssemblyHandler)
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
        public IEnumerable<(Type, string)> DiscoverScriptPlugins()
        {
            var pluginDir = $"{Utilities.OperatingDirectory}{PluginDir}{Path.DirectorySeparatorChar}";

            if (!Directory.Exists(pluginDir))
            {
                return Enumerable.Empty<(Type, string)>();
            }

            var scriptPluginFiles =
                Directory.GetFiles(pluginDir, "*.js").AsEnumerable().Union(GetRemoteScripts()).ToList();

            var bothVersionPlugins = scriptPluginFiles.Select(fileName =>
            {
                _logger.LogDebug("Discovered script plugin {FileName}", fileName);
                try
                {
                    var fileContents = File.ReadAllLines(fileName);
                    var isValidV2 = fileContents.Any(line => Regex.IsMatch(line, PluginV2Match));
                    return isValidV2 ? (typeof(IPluginV2), fileName) : (typeof(IPlugin), fileName);
                }
                catch
                {
                    return (typeof(IPlugin), fileName);
                }
            }).ToList();

            return bothVersionPlugins;
        }

        /// <summary>
        /// discovers all the C# assembly plugins and commands
        /// </summary>
        /// <returns></returns>
        public (IEnumerable<Type>, IEnumerable<Type>, IEnumerable<Type>) DiscoverAssemblyPluginImplementations()
        {
            var pluginDir = $"{Utilities.OperatingDirectory}{PluginDir}{Path.DirectorySeparatorChar}";
            var pluginTypes = new List<Type>();
            var commandTypes = new List<Type>();
            var configurationTypes = new List<Type>();

            if (!Directory.Exists(pluginDir))
            {
                return (pluginTypes, commandTypes, configurationTypes);
            }

            var dllFileNames = Directory.GetFiles(pluginDir, "*.dll");
            _logger.LogDebug("Discovered {Count} potential plugin assemblies", dllFileNames.Length);

            if (!dllFileNames.Any())
            {
                return (pluginTypes, commandTypes, configurationTypes);
            }

            // we only want to load the most recent assembly in case of duplicates
            var assemblies = dllFileNames.Select(Assembly.LoadFrom)
                .Union(GetRemoteAssemblies())
                .GroupBy(assembly => assembly.FullName).Select(assembly =>
                    assembly.OrderByDescending(asm => asm.GetName().Version).First());

            var eligibleAssemblyTypes = assemblies
                .SelectMany(asm =>
                {
                    try
                    {
                        return asm.GetTypes();
                    }
                    catch
                    {
                        return Enumerable.Empty<Type>();
                    }
                }).Where(type =>
                    FilterTypes.Any(filterType => type.GetInterface(filterType.Name, false) != null) ||
                    (type.IsClass && FilterTypes.Contains(type.BaseType)));
            
            foreach (var assemblyType in eligibleAssemblyTypes)
            {
                var isPlugin =
                    (assemblyType.GetInterface(nameof(IPlugin), false) ??
                     assemblyType.GetInterface(nameof(IPluginV2), false)) != null &&
                    (!assemblyType.Namespace?.StartsWith(nameof(SharedLibraryCore)) ?? false);

                if (isPlugin)
                {
                    pluginTypes.Add(assemblyType);
                    continue;
                }

                var isCommand = assemblyType.IsClass && assemblyType.BaseType == typeof(Command) &&
                                (!assemblyType.Namespace?.StartsWith(nameof(SharedLibraryCore)) ?? false);

                if (isCommand)
                {
                    commandTypes.Add(assemblyType);
                    continue;
                }

                var isConfiguration = assemblyType.IsClass &&
                                      assemblyType.GetInterface(nameof(IBaseConfiguration), false) != null &&
                                      (!assemblyType.Namespace?.StartsWith(nameof(SharedLibraryCore)) ?? false);

                if (isConfiguration)
                {
                    configurationTypes.Add(assemblyType);
                }
            }

            _logger.LogDebug("Discovered {Count} plugin implementations", pluginTypes.Count);
            _logger.LogDebug("Discovered {Count} plugin command implementations", commandTypes.Count);
            _logger.LogDebug("Discovered {Count} plugin configuration implementations", configurationTypes.Count);

            return (pluginTypes, commandTypes, configurationTypes);
        }

        private IEnumerable<Assembly> GetRemoteAssemblies()
        {
            try
            {
                _pluginSubscription ??= _masterApi
                    .GetPluginSubscription(Guid.Parse(_appConfig.Id), _appConfig.SubscriptionId).Result;

                return _remoteAssemblyHandler.DecryptAssemblies(_pluginSubscription
                    .Where(sub => sub.Type == PluginType.Binary).Select(sub => sub.Content).ToArray());
            }

            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load remote assemblies");
                return Enumerable.Empty<Assembly>();
            }
        }

        private IEnumerable<string> GetRemoteScripts()
        {
            try
            {
                _pluginSubscription ??= _masterApi
                    .GetPluginSubscription(Guid.Parse(_appConfig.Id), _appConfig.SubscriptionId).Result;

                return _remoteAssemblyHandler.DecryptScripts(_pluginSubscription
                    .Where(sub => sub.Type == PluginType.Script).Select(sub => sub.Content).ToArray());
            }

            catch (Exception ex)
            {
                _logger.LogWarning(ex,"Could not load remote scripts");
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
