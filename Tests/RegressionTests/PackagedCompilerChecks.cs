using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging.Abstractions;

internal static class PackagedCompilerChecks
{
    internal static async Task Run(string packageDirectory)
    {
        var context = new PackageContext(Path.Combine(Path.GetFullPath(packageDirectory), "Lib"));
        try
        {
            var application = context.LoadFromAssemblyPath(Path.Combine(context.LibraryDirectory, "IW4MAdmin.dll"));
            var compilerType = application.GetType("IW4MAdmin.Application.Plugin.CSharpScript.CsPluginCompiler", true)!;
            var loggerType = typeof(NullLogger<>).MakeGenericType(compilerType);
            var logger = loggerType.GetField("Instance")!.GetValue(null);
            var compiler = Activator.CreateInstance(compilerType, logger)!;
            var loadContextType = application.GetType("IW4MAdmin.Application.Plugin.CSharpScript.CsPluginLoadContext", true)!;
            foreach (var file in Directory.EnumerateFiles(Path.Combine(Check.RepositoryRoot, "Plugins", "ScriptPlugins"), "*.cs"))
            {
                var scriptContext = (AssemblyLoadContext)Activator.CreateInstance(loadContextType)!;
                try { await (Task<Assembly>)compilerType.GetMethod("CompileFromFile")!.Invoke(compiler, [file, scriptContext])!; }
                finally { scriptContext.Unload(); }
            }
            Console.WriteLine("PASS: all C# scripts compile using the cleaned publish package");
        }
        finally { context.Unload(); }
    }

    private sealed class PackageContext(string directory) : AssemblyLoadContext(isCollectible: true)
    {
        internal string LibraryDirectory => directory;
        protected override Assembly? Load(AssemblyName name)
        {
            if (name.Name is "IW4MAdmin" or "SharedLibraryCore" or "Data" || name.Name!.StartsWith("Microsoft.CodeAnalysis"))
            {
                var path = Path.Combine(directory, name.Name + ".dll");
                if (!File.Exists(path)) throw new FileNotFoundException("Missing packaged runtime dependency", path);
                return LoadFromAssemblyPath(path);
            }
            return null;
        }
    }
}
