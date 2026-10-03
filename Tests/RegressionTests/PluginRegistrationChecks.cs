using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Localization;

/// <summary>
/// A plugin that fails to register (e.g. built against a newer IW4MAdmin) must not take its
/// commands with it into the container: they need services it never registered, and resolving
/// the command list would then stop the host from starting.
/// </summary>
internal static class PluginRegistrationChecks
{
    private static readonly MethodInfo RegisterPluginImplementations = typeof(IW4MAdmin.Application.Program)
        .GetMethod("RegisterPluginImplementations", BindingFlags.NonPublic | BindingFlags.Static)!;

    public static void Run()
    {
        var previousLocalization = Utilities.CurrentLocalization;
        Utilities.CurrentLocalization = new Layout(new Dictionary<string, string>
        {
            ["PLUGIN_IMPORTER_NEWER_API"] = "{{plugin}} needs a newer IW4MAdmin"
        });

        try
        {
            foreach (var pluginType in new[] { typeof(NewerApiPlugin), typeof(FailingPlugin) })
            {
                var services = BaseServices();
                var rejected = Register(services, pluginType);
                using var provider = services.BuildServiceProvider();

                Check.Equal(true, rejected.Contains(pluginType.Assembly),
                    $"{pluginType.Name}: plugin is reported as unavailable");
                Check.Equal(0, provider.GetServices<IManagerCommand>().Count(),
                    $"{pluginType.Name}: its commands are left out, so the command list still resolves");
                Check.Equal(typeof(UnavailablePlugin), provider.GetServices<IPluginV2>().Single().GetType(),
                    $"{pluginType.Name}: plugin is replaced by the inert placeholder");
                Check.Equal(null, provider.GetService<PluginService>(),
                    $"{pluginType.Name}: services registered before the failure are undone");
            }

            var workingServices = BaseServices();
            var noneRejected = Register(workingServices, typeof(WorkingPlugin));
            using var workingProvider = workingServices.BuildServiceProvider();
            Check.Equal(0, noneRejected.Count, "A plugin that registers cleanly is not rejected");
            Check.Equal(typeof(PluginCommand), workingProvider.GetServices<IManagerCommand>().Single().GetType(),
                "A plugin that registers cleanly keeps its commands");
            Check.Equal(typeof(WorkingPlugin), workingProvider.GetServices<IPluginV2>().Single().GetType(),
                "A plugin that registers cleanly is constructed normally");
        }
        finally
        {
            Utilities.CurrentLocalization = previousLocalization;
        }
    }

    private static IServiceCollection BaseServices() => new ServiceCollection()
        .AddSingleton(new CommandConfiguration())
        .AddSingleton<ITranslationLookup>(new TranslationLookup { Set = new Dictionary<string, string>() });

    private static HashSet<Assembly> Register(IServiceCollection services, Type pluginType) =>
        (HashSet<Assembly>)RegisterPluginImplementations.Invoke(null,
            [services, new[] { pluginType }, new[] { typeof(PluginCommand) }, Array.Empty<Type>(),
                NullLogger.Instance])!;

    internal sealed class PluginService;

    internal sealed class PluginCommand(CommandConfiguration config, ITranslationLookup layout, PluginService service)
        : Command(config, layout)
    {
        public PluginService Service { get; } = service;
        public override Task ExecuteAsync(GameEvent gameEvent) => Task.CompletedTask;
    }

    internal sealed class NewerApiPlugin : IPluginV2
    {
        public string Name => nameof(NewerApiPlugin);
        public string Author => "regression";
        public string Version => "1";

        // what an older host sees when a plugin calls API it doesn't have
        public static void RegisterDependencies(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<PluginService>();
            throw new MissingMethodException("SharedLibraryCore.Interfaces.IFutureApi", "Register");
        }
    }

    internal sealed class FailingPlugin : IPluginV2
    {
        public string Name => nameof(FailingPlugin);
        public string Author => "regression";
        public string Version => "1";

        public static void RegisterDependencies(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<PluginService>();
            throw new InvalidOperationException("registration failed");
        }
    }

    internal sealed class WorkingPlugin : IPluginV2
    {
        public string Name => nameof(WorkingPlugin);
        public string Author => "regression";
        public string Version => "1";

        public static void RegisterDependencies(IServiceCollection serviceCollection) =>
            serviceCollection.AddSingleton<PluginService>();
    }
}
