using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace SharedLibraryCore.Helpers;

/// <summary>
/// helpers for tolerating plugins that were compiled against a newer IW4MAdmin API than the
/// running instance provides. Lives in SharedLibraryCore so both the host (Application) and the
/// central event dispatch can soft-fail consistently with a single friendly notice per plugin.
/// </summary>
public static class PluginApiCompatibility
{
    // plugins already reported this process, so the notice fires once even though events fire often
    private static readonly HashSet<string> Reported = [];
    private static readonly Lock ReportLock = new();

    /// <summary>
    /// exception types raised when an assembly references a type/member that does not exist in the
    /// currently loaded SharedLibraryCore (i.e. the plugin needs a newer IW4MAdmin)
    /// </summary>
    private static bool IsMissingApiCore(Exception exception) => exception is TypeLoadException
        or MissingMethodException or MissingFieldException or MissingMemberException
        or FileNotFoundException or FileLoadException;

    /// <summary>
    /// true when the exception (or any inner / loader exception) indicates the plugin needs a newer
    /// IW4MAdmin API than is available
    /// </summary>
    public static bool IsMissingApiException(Exception exception)
    {
        while (exception is not null)
        {
            if (IsMissingApiCore(exception) ||
                (exception is ReflectionTypeLoadException rtle &&
                 rtle.LoaderExceptions.Any(loaderException =>
                     loaderException is not null && IsMissingApiCore(loaderException))))
            {
                return true;
            }

            exception = exception.InnerException;
        }

        return false;
    }

    /// <summary>
    /// gets the loadable types from an assembly, tolerating plugins built against a newer API.
    /// Types whose signatures resolve are returned and still load; types that need unavailable API
    /// are dropped and a single friendly notice is surfaced per assembly.
    /// </summary>
    public static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            if (ex.LoaderExceptions.Any(loaderException =>
                    loaderException is not null && IsMissingApiCore(loaderException)))
            {
                NotifyNewerApiRequired(assembly);
            }

            // surface the types that did resolve so plugins that don't actually use the missing
            // API continue to load normally
            return ex.Types.Where(type => type is not null)!;
        }
        catch (Exception ex) when (IsMissingApiException(ex))
        {
            NotifyNewerApiRequired(assembly);
            return [];
        }
    }

    /// <summary>
    /// walks an exception (and its inner exceptions) to find the assembly whose code raised it,
    /// so a runtime missing-API failure can be attributed to the offending plugin
    /// </summary>
    public static Assembly TryGetOffendingAssembly(Exception exception)
    {
        while (exception is not null)
        {
            var assembly = exception.TargetSite?.DeclaringType?.Assembly;
            if (assembly is not null)
            {
                return assembly;
            }

            exception = exception.InnerException;
        }

        return null;
    }

    /// <summary>
    /// emits a user-facing notice (console + log) that a plugin could not be loaded/run because it
    /// targets a newer IW4MAdmin API. Reports at most once per plugin for the life of the process.
    /// Logging goes through <see cref="Utilities.DefaultLogger"/> — the ambient logger IW4MAdmin
    /// provides for code not created by dependency injection. That is deliberate: the call sites are
    /// the static event dispatch (<c>EventExtensions</c>) and registration-time plugin discovery,
    /// neither of which has a DI scope to inject an <c>ILogger</c> from.
    /// </summary>
    public static void NotifyNewerApiRequired(Assembly assembly, string displayName = null)
    {
        var key = assembly?.FullName ?? displayName;
        if (key is not null)
        {
            lock (ReportLock)
            {
                if (!Reported.Add(key))
                {
                    return;
                }
            }
        }

        var pluginName = displayName ?? assembly?.GetName().Name ?? "A plugin";

        // user-facing console notice is localized; the log line below stays hardcoded English
        Console.WriteLine(
            $"[Plugin] {Utilities.CurrentLocalization.LocalizationIndex["PLUGIN_IMPORTER_NEWER_API"].FormatExt(pluginName)}");
        Utilities.DefaultLogger?.LogWarning(
            "{Plugin} could not be fully loaded because it targets a newer IW4MAdmin API than this instance provides",
            pluginName);
    }
}
