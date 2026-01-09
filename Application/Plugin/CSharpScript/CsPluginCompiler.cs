using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;

namespace IW4MAdmin.Application.Plugin.CSharpScript;

/// <summary>
/// Handles Roslyn-based compilation of .cs plugin files.
/// </summary>
public class CsPluginCompiler
{
    private readonly ILogger<CsPluginCompiler> _logger;
    private readonly Lazy<IEnumerable<MetadataReference>> _metadataReferences;

    public CsPluginCompiler(ILogger<CsPluginCompiler> logger)
    {
        _logger = logger;
        _metadataReferences = new Lazy<IEnumerable<MetadataReference>>(GetMetadataReferences);
    }

    /// <summary>
    /// Compiles a .cs file and loads the assembly into the given context.
    /// </summary>
    /// <param name="csFilePath">Path to the .cs file</param>
    /// <param name="loadContext">The AssemblyLoadContext to load into</param>
    /// <returns>The compiled and loaded assembly</returns>
    public Assembly CompileFromFile(string csFilePath, CsPluginLoadContext loadContext)
    {
        var absolutePath = Path.GetFullPath(csFilePath);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException($"Plugin file not found: {absolutePath}");
        }

        _logger.LogDebug("Compiling C# plugin from {Path}", absolutePath);

        var sourceCode = File.ReadAllText(absolutePath);
        
        // Comment out #:package directives used for IntelliSense (not valid C# syntax)
        sourceCode = PreprocessSource(sourceCode);
        
        var assemblyName = Path.GetFileNameWithoutExtension(absolutePath);

        return CompileSource(sourceCode, assemblyName, loadContext);
    }

    /// <summary>
    /// Preprocesses the source code to handle special directives.
    /// </summary>
    private static string PreprocessSource(string sourceCode)
    {
        // Comment out #:package directives - these are used by editors for IntelliSense
        // but aren't valid C# syntax. The pattern matches lines like:
        // #:package SomePackage
        // #:package SomePackage@1.0.0
        return sourceCode.Replace("#:package", "//#:package");
    }

    /// <summary>
    /// Pre-compiles a .cs file to analyze the plugin type without a specific context.
    /// Used during startup to invoke RegisterDependencies before DI container is built.
    /// </summary>
    /// <param name="csFilePath">Path to the .cs file</param>
    /// <returns>The compiled assembly (loaded into a temporary collectible context)</returns>
    public Assembly PreCompile(string csFilePath)
    {
        var tempContext = new CsPluginLoadContext();
        return CompileFromFile(csFilePath, tempContext);
    }

    private Assembly CompileSource(string sourceCode, string assemblyName, CsPluginLoadContext loadContext)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: [syntaxTree],
            references: _metadataReferences.Value,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithPlatform(Platform.AnyCpu));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            var failures = result.Diagnostics
                .Where(d => d.IsWarningAsError || d.Severity == DiagnosticSeverity.Error)
                .Select(d =>
                {
                    var lineSpan = d.Location.GetLineSpan();
                    var line = lineSpan.StartLinePosition.Line + 1;
                    var column = lineSpan.StartLinePosition.Character + 1;
                    return $"  [{line}:{column}] {d.Id}: {d.GetMessage()}";
                })
                .ToList();

            var errorMessage = $"C# plugin compilation failed:\n{string.Join("\n", failures)}";
            _logger.LogError("Compilation failed for {AssemblyName}:\n{Errors}", assemblyName, string.Join("\n", failures));
            throw new InvalidOperationException(errorMessage);
        }

        _logger.LogDebug("Compilation successful for {AssemblyName}", assemblyName);

        ms.Seek(0, SeekOrigin.Begin);
        return loadContext.LoadFromStream(ms);
    }

    private IEnumerable<MetadataReference> GetMetadataReferences()
    {
        var assemblies = new List<MetadataReference>();

        // Add core runtime references from trusted platform assemblies
        var trustedAssemblies =
            ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator)
            ?? [];

        foreach (var assemblyPath in trustedAssemblies)
        {
            if (File.Exists(assemblyPath))
            {
                assemblies.Add(MetadataReference.CreateFromFile(assemblyPath));
            }
        }

        // Add reference to SharedLibraryCore (for IPluginV2, etc.)
        AddAssemblyReference(assemblies, typeof(SharedLibraryCore.Interfaces.IPluginV2).Assembly);

        // Add reference to Data assembly
        AddAssemblyReference(assemblies, typeof(Data.Abstractions.IDatabaseContextFactory).Assembly);

        _logger.LogDebug("Loaded {Count} metadata references for compilation", assemblies.Count);

        return assemblies;
    }

    private static void AddAssemblyReference(List<MetadataReference> assemblies, Assembly assembly)
    {
        var location = assembly.Location;
        if (!string.IsNullOrEmpty(location) && File.Exists(location))
        {
            assemblies.Add(MetadataReference.CreateFromFile(location));
        }
    }
}
