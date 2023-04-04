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
        private static readonly string PluginDir = "Plugins";
        private const string PluginV2Match = "^ *((?:var|const|let) +init)|function init";
        private readonly ILogger _logger;
        private readonly IRemoteAssemblyHandler _remoteAssemblyHandler;
        private readonly IMasterApi _masterApi;
        private readonly ApplicationConfiguration _appConfig;

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
            var pluginTypes = Enumerable.Empty<Type>();
            var commandTypes = Enumerable.Empty<Type>();
            var configurationTypes = Enumerable.Empty<Type>();

            if (Directory.Exists(pluginDir))
            {
                var dllFileNames = Directory.GetFiles(pluginDir, "*.dll");
                _logger.LogDebug("Discovered {Count} potential plugin assemblies", dllFileNames.Length);

                if (dllFileNames.Length > 0)
                {
                    // we only want to load the most recent assembly in case of duplicates
                    var assemblies = dllFileNames.Select(name => Assembly.LoadFrom(name))
                        .Union(GetRemoteAssemblies())
                        .GroupBy(assembly => assembly.FullName).Select(assembly => assembly.OrderByDescending(asm => asm.GetName().Version).First());

                    pluginTypes = assemblies
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
                        })
                        .Where(assemblyType => (assemblyType.GetInterface(nameof(IPlugin), false) ?? assemblyType.GetInterface(nameof(IPluginV2), false)) != null)
                        .Where(assemblyType => !assemblyType.Namespace?.StartsWith(nameof(SharedLibraryCore)) ?? false);

                    _logger.LogDebug("Discovered {count} plugin implementations", pluginTypes.Count());

                    commandTypes = assemblies
                        .SelectMany(asm =>{
                            try
                            {
                                return asm.GetTypes();
                            }
                            catch
                            {
                                return Enumerable.Empty<Type>();
                            }
                        })
                        .Where(assemblyType => assemblyType.IsClass && assemblyType.BaseType == typeof(Command))
                        .Where(assemblyType => !assemblyType.Namespace?.StartsWith(nameof(SharedLibraryCore)) ?? false);

                    _logger.LogDebug("Discovered {Count} plugin commands", commandTypes.Count());

                    configurationTypes = assemblies
                        .SelectMany(asm => {
                            try
                            {
                                return asm.GetTypes();
                            }
                            catch
                            {
                                return Enumerable.Empty<Type>();
                            }
                        })
                        .Where(asmType =>
                            asmType.IsClass && asmType.GetInterface(nameof(IBaseConfiguration), false) != null)
                        .Where(assemblyType => !assemblyType.Namespace?.StartsWith(nameof(SharedLibraryCore)) ?? false);

                    _logger.LogDebug("Discovered {Count} configuration implementations", configurationTypes.Count());
                }
            }

            return (pluginTypes, commandTypes, configurationTypes);
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
                _logger.LogWarning(ex, "Could not load remote assemblies");
                return Enumerable.Empty<Assembly>();
            }
        }

        private IEnumerable<string> GetRemoteScripts()
        {
            try
            {
                _pluginSubscription ??= _masterApi.GetPluginSubscription(Guid.Parse(_appConfig.Id), _appConfig.SubscriptionId).Result;

                return _remoteAssemblyHandler.DecryptScripts(_pluginSubscription.Where(sub => sub.Type == PluginType.Script).Select(sub => sub.Content).ToArray());
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
