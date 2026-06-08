using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using IW4MAdmin.Application.API.Master;
#if DEBUG
using Microsoft.Extensions.DependencyModel;
#endif
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Plugins;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Plugin
{
    /// <summary>
    /// implementation of IPluginImporter
    /// discovers plugins and script plugins
    /// </summary>
    public class PluginImporter(
        ILogger<PluginImporter> logger,
        ApplicationConfiguration appConfig,
        IMasterApi masterApi,
        IRemoteAssemblyHandler remoteAssemblyHandler,
        IPluginBundleLoader bundleLoader)
        : IPluginImporter
    {
        // Memoized once and shared by the three remote-content fetches (scripts, binaries, bundles). Those
        // run sequentially today (LINQ Union enumeration on a single thread), but DiscoverScriptPlugins and
        // DiscoverAssemblyPluginImplementations are separate public entry points that could be called from
        // different threads, so the fetch is guarded. PublicationOnly is deliberate: it does not cache a
        // failed fetch, so a transient master outage during one pass can still be retried by the next.
        private readonly Lazy<IEnumerable<PluginSubscriptionContent>> _pluginSubscription = new(
            () => masterApi.GetPluginSubscription(appConfig.Id, appConfig.SubscriptionId).Result,
            LazyThreadSafetyMode.PublicationOnly);

        private const string PluginDir = "Plugins";
        private const string RemoteBundleSource = "remote bundle";
        private const string PluginV2Match = "^ *((?:var|const|let) +init)|function init";
        private readonly ILogger _logger = logger;

        private static readonly Type[] FilterTypes =
        {
            typeof(IPlugin),
            typeof(IPluginV2),
            typeof(Command),
            typeof(IBaseConfiguration)
        };

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
                Directory.GetFiles(pluginDir, "*.js")
                    .Where(jsFile =>
                    {
                        var csFile = Path.ChangeExtension(jsFile, ".cs");
                        if (!File.Exists(csFile)) return true;
                        _logger.LogInformation("Skipping {JsPlugin} — superseded by {CsPlugin}",
                            Path.GetFileName(jsFile), Path.GetFileName(csFile));
                        return false;
                    })
                    .Union(GetRemoteScripts()).ToList();

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
        private IReadOnlyList<Assembly>? _pluginAssemblies;

        /// <summary>
        /// The distilled set of plugin assemblies — loose DLLs, local and remote bundles, and remote binaries,
        /// deduped to the highest version of each. Computed once and cached. The Application discovery pass and
        /// the WebfrontCore startup (which resolves this same importer from DI to register plugin MVC parts and
        /// routable Blazor components) therefore share one result rather than each enumerating the plugins dir.
        /// </summary>
        public IEnumerable<Assembly> DiscoverPluginAssemblies() => _pluginAssemblies ??= DistillPluginAssemblies();

        /// <summary>One discovered plugin assembly together with where it was ingested from, for diagnostics.</summary>
        private readonly record struct PluginAssemblyCandidate(Assembly Assembly, string Ingest, string Source);

        private IReadOnlyList<Assembly> DistillPluginAssemblies()
        {
            var pluginDir = $"{Utilities.OperatingDirectory}{PluginDir}{Path.DirectorySeparatorChar}";
            if (!Directory.Exists(pluginDir))
            {
                return [];
            }

            // route the shared bundle loader's debug logging through our logger so bundle loads (and, crucially,
            // cache hits — first loader of an id wins) are traceable alongside this provenance log.
            if (bundleLoader is PluginBundleLoader concreteLoader)
            {
                concreteLoader.Logger = _logger;
            }

            var dllFileNames = Directory.GetFiles(pluginDir, "*.dll");
            _logger.LogDebug("Discovered {Count} potential plugin assemblies", dllFileNames.Length);

#if DEBUG
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies().Select(asm => asm.GetName().Name);
            var references = DependencyContext.Default?.GetDefaultAssemblyNames().Select(x => x.Name) ?? [];
            var validAssemblies = references.Except(loadedAssemblies)
                .Where(asm => !asm.StartsWith("Microsoft"))
                .Where(asm => !asm.StartsWith("System"))
                .Distinct();
            var reloadableAssemblies = validAssemblies.Select(Assembly.Load);
#endif

            // Collect every candidate WITH where it came from, so a confusing "which copy actually loaded?" can
            // be answered from the log: each ingest is recorded, then the dedup below logs the winner per identity.
            var candidates = new List<PluginAssemblyCandidate>();

            foreach (var dll in dllFileNames)
            {
                AddCandidate(candidates, Assembly.LoadFrom(dll), "loose-dll(disk)", dll);
            }

            candidates.AddRange(GetBundleCandidates(pluginDir));
            candidates.AddRange(GetRemoteBundleCandidates());

            foreach (var assembly in GetRemoteAssemblies())
            {
                AddCandidate(candidates, assembly, "remote-store-binary", "remote store (content type: dll)");
            }

#if DEBUG
            foreach (var assembly in reloadableAssemblies)
            {
                AddCandidate(candidates, assembly, "debug-reloadable", "DependencyContext (DEBUG)");
            }
#endif

            // we only want to load the most recent assembly in case of duplicates
            return candidates
                .GroupBy(candidate => candidate.Assembly.FullName)
                .Select(SelectHighestVersion)
                .Select(candidate => candidate.Assembly)
                .ToList();
        }

        /// <summary>Records a discovered candidate and logs where it came from (debug).</summary>
        private void AddCandidate(List<PluginAssemblyCandidate> candidates, Assembly assembly, string ingest, string source)
        {
            var name = assembly.GetName();
            candidates.Add(new PluginAssemblyCandidate(assembly, ingest, source));
            _logger.LogDebug("Plugin assembly candidate {Name} v{Version} — ingest: {Ingest}, source: {Source}",
                name.Name, name.Version, ingest, source);
        }

        /// <summary>
        /// Picks the highest-versioned candidate for one assembly identity (unchanged selection behaviour) and
        /// logs the decision — listing every competing source when more than one copy of the same plugin was found.
        /// </summary>
        private PluginAssemblyCandidate SelectHighestVersion(IGrouping<string?, PluginAssemblyCandidate> group)
        {
            var ordered = group.OrderByDescending(candidate => candidate.Assembly.GetName().Version).ToList();
            var winner = ordered[0];
            var name = winner.Assembly.GetName();

            if (ordered.Count > 1)
            {
                _logger.LogDebug(
                    "Multiple copies of {Name} found; selected v{Version} via {Ingest} from {Source}. Candidates: {Candidates}",
                    name.Name, name.Version, winner.Ingest, winner.Source,
                    string.Join(" || ", ordered.Select(c => $"v{c.Assembly.GetName().Version} [{c.Ingest}] {c.Source}")));
            }
            else
            {
                _logger.LogDebug("Loaded plugin assembly {Name} v{Version} via {Ingest} from {Source}",
                    name.Name, name.Version, winner.Ingest, winner.Source);
            }

            return winner;
        }

        public (IEnumerable<Type>, IEnumerable<Type>, IEnumerable<Type>) DiscoverAssemblyPluginImplementations()
        {
            var pluginTypes = new List<Type>();
            var commandTypes = new List<Type>();
            var configurationTypes = new List<Type>();

            var assemblies = DiscoverPluginAssemblies();
            if (!assemblies.Any())
            {
                return (pluginTypes, commandTypes, configurationTypes);
            }

            var eligibleAssemblyTypes = assemblies.Concat(AppDomain.CurrentDomain.GetAssemblies()
                    .Where(asm => !new[] { "IW4MAdmin", "SharedLibraryCore", "Stats" }.Contains(asm.GetName().Name)))
                // a plugin loaded from the Plugins dir is also present in the AppDomain, so the
                // concat above lists it twice; dedupe by identity so each plugin type is only
                // discovered (and later registered/instantiated) once
                .DistinctBy(asm => asm.FullName)
                .SelectMany(PluginApiCompatibility.GetLoadableTypes)
                .Where(type =>
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
                return remoteAssemblyHandler.DecryptAssemblies(_pluginSubscription.Value
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
                return remoteAssemblyHandler.DecryptScripts(_pluginSubscription.Value
                    .Where(sub => sub.Type == PluginType.Script).Select(sub => sub.Content).ToArray());
            }

            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load remote scripts");
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// Loads plugin bundles streamed from the master (premium subscription channel). The encrypted
        /// payloads are decrypted to raw zip bytes, loaded in-memory through the same shared loader the
        /// local-disk path uses (so the assembly identity matches the one Razor discovery resolves from
        /// the cache), and their game scripts are extracted to disk. The decrypted zip plaintext is zeroed
        /// once the loader has consumed it so a runnable copy of the premium DLL doesn't linger on the heap.
        /// </summary>
        private List<PluginAssemblyCandidate> GetRemoteBundleCandidates()
        {
            var candidates = new List<PluginAssemblyCandidate>();

            try
            {
                var encryptedBundles = _pluginSubscription.Value
                    .Where(sub => sub.Type == PluginType.Bundle).Select(sub => sub.Content).ToArray();

                if (encryptedBundles.Length is 0)
                {
                    return candidates;
                }

                foreach (var zipBytes in remoteAssemblyHandler.DecryptContent(encryptedBundles))
                {
                    try
                    {
                        var byteCount = zipBytes.Length;
                        var bundle = bundleLoader.LoadFromZipBytes(zipBytes, RemoteBundleSource);
                        if (bundle is null)
                        {
                            continue;
                        }

                        ExtractBundleGameScripts(bundle);
                        AddCandidate(candidates, bundle.Assembly, "remote-store-bundle",
                            $"remote store (content type: zip, {byteCount:N0} bytes, bundle id '{bundle.Id}', manifest version '{bundle.Manifest.Version}')");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not load a remote plugin bundle");
                    }
                    finally
                    {
                        // the loader has copied the entry assembly + web assets into its own buffers,
                        // so drop the decrypted archive plaintext from the heap (premium anti-extraction)
                        CryptographicOperations.ZeroMemory(zipBytes);
                    }
                }

                return candidates;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load remote plugin bundles");
                return candidates;
            }
        }

        /// <summary>
        /// Loads plugin bundles (*.zip or pre-unpacked folders) from the plugins dir via the shared loader
        /// (which caches by id, so the assembly identity matches the one already loaded for Razor discovery)
        /// and extracts any bundled game scripts to disk.
        /// </summary>
        private List<PluginAssemblyCandidate> GetBundleCandidates(string pluginDir)
        {
            var candidates = new List<PluginAssemblyCandidate>();

            foreach (var source in EnumerateBundleSources(pluginDir))
            {
                try
                {
                    var bundle = source.IsZip
                        ? bundleLoader.LoadFromZipBytes(File.ReadAllBytes(source.Path), source.Path)
                        : bundleLoader.LoadFromDirectory(source.Path);

                    if (bundle is null)
                    {
                        continue;
                    }

                    ExtractBundleGameScripts(bundle);
                    AddCandidate(candidates, bundle.Assembly,
                        source.IsZip ? "disk-bundle-zip" : "disk-bundle-dir",
                        $"{source.Path} (bundle id '{bundle.Id}', manifest version '{bundle.Manifest.Version}')");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load plugin bundle {Source}", source.Path);
                }
            }

            return candidates;
        }

        private readonly record struct BundleSource(string Path, bool IsZip);

        /// <summary>
        /// Read-only discovery of plugin bundles directly under <paramref name="pluginDir"/>: every *.zip
        /// file, and every immediate subdirectory containing a manifest.json at its root (pre-unpacked dev
        /// folder). Performs no extraction.
        /// </summary>
        private static IEnumerable<BundleSource> EnumerateBundleSources(string pluginDir)
        {
            if (!Directory.Exists(pluginDir))
            {
                yield break;
            }

            foreach (var zip in Directory.EnumerateFiles(pluginDir, "*.zip", SearchOption.TopDirectoryOnly))
            {
                yield return new BundleSource(zip, IsZip: true);
            }

            foreach (var dir in Directory.EnumerateDirectories(pluginDir))
            {
                if (File.Exists(Path.Combine(dir, "manifest.json")))
                {
                    yield return new BundleSource(dir, IsZip: false);
                }
            }
        }

        /// <summary>
        /// Writes a bundle's GSC files to the configured game-files folder. GSC is the only bundle content
        /// written to disk; when no path is configured, extraction is skipped.
        /// </summary>
        private void ExtractBundleGameScripts(LoadedBundle bundle)
        {
            if (bundle.GscFiles.Count == 0)
            {
                return;
            }

            var targetPath = appConfig.PluginGscExtractPath;
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                _logger.LogDebug(
                    "Bundle {Id} ships {Count} GSC file(s) but PluginGscExtractPath is not configured; skipping extraction",
                    bundle.Id, bundle.GscFiles.Count);
                return;
            }

            foreach (var (relativePath, content) in bundle.GscFiles)
            {
                var destination = Path.Combine(targetPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.WriteAllBytes(destination, content);
            }

            _logger.LogInformation("Extracted {Count} GSC file(s) from bundle {Id} to {Path}",
                bundle.GscFiles.Count, bundle.Id, targetPath);
        }
    }

    public enum PluginType
    {
        Binary,
        Script,
        CSharpScript,
        Bundle
    }
}
