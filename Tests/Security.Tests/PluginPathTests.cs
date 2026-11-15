using System.Reflection;
using IW4MAdmin.Application.Plugin;
using SharedLibraryCore.Helpers;

namespace Security.Tests;

/// <summary>
/// S1/S8: path containment for what a plugin bundle or a plugin-supplied name can write to disk.
/// Everything is written under a throwaway directory in the temp folder.
/// </summary>
public class PluginPathTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "iw4m-sec-" + Guid.NewGuid().ToString("N"));

    public PluginPathTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, true);

    private static List<string> WriteBundleFiles(Dictionary<string, byte[]> files, string target) =>
        (List<string>)typeof(PluginImporter).GetMethod("WriteBundleFiles", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [files, target, true])!;

    public static TheoryData<string> EscapingEntries => new()
    {
        "../escaped.gsc",
        "..\\escaped.gsc",
        "sub/../../escaped.gsc",
        // sibling directory that shares the target's name as a prefix
        "../extract-sibling/escaped.gsc",
        "../../escaped.gsc"
    };

    [Theory]
    [MemberData(nameof(EscapingEntries))]
    public void Bundle_entry_cannot_be_written_outside_its_extraction_folder(string entry)
    {
        var target = Path.Combine(_root, "plugins", "extract");
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(Path.Combine(_root, "plugins", "extract-sibling"));

        var skipped = WriteBundleFiles(new Dictionary<string, byte[]>
        {
            [entry] = "X"u8.ToArray(),
            ["sub/kept.gsc"] = "Y"u8.ToArray()
        }, target);

        var written = Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(target, file)).ToList();
        Assert.Equal([Path.Combine("sub", "kept.gsc")], written);
        Assert.Equal([entry], skipped);
    }

    [Fact]
    public void Bundle_entry_with_an_absolute_path_is_not_written()
    {
        var target = Path.Combine(_root, "extract");
        var outside = Path.Combine(_root, "outside.gsc");
        Directory.CreateDirectory(target);

        var skipped = WriteBundleFiles(new Dictionary<string, byte[]> { [outside] = "X"u8.ToArray() }, target);

        Assert.False(File.Exists(outside));
        Assert.Single(skipped);
    }

    public static TheoryData<string> HostileNames => new()
    {
        "../../outside",
        "..\\..\\outside",
        "a/../../../outside",
        "....//....//outside",
        "/outside",
        "\\\\server\\share\\outside",
        ". ./. ./outside"
    };

    /// <summary>AddDatabase / AddConfiguration names (docs/plugin-data-directories.md, section 4).</summary>
    [Theory]
    [MemberData(nameof(HostileNames))]
    public void Plugin_supplied_database_or_config_name_stays_inside_the_plugin_folder(string name)
    {
        var pluginRoot = Path.Combine(_root, "Plugins", "Victim");
        Directory.CreateDirectory(pluginRoot);
        string? resolved = null;

        try
        {
            resolved = PluginDirectoryResolver.CombineWithinRoot(pluginRoot, name, ".db");
        }
        catch (InvalidOperationException)
        {
            // refusing is an acceptable outcome
        }

        if (resolved is not null)
        {
            Assert.StartsWith(Path.GetFullPath(pluginRoot) + Path.DirectorySeparatorChar, resolved);
        }

        // nothing, not even a directory, may have been created beside or above the plugin folder
        Assert.Equal([pluginRoot], Directory.GetDirectories(Path.Combine(_root, "Plugins")));
        Assert.Equal([Path.Combine(_root, "Plugins")], Directory.GetFileSystemEntries(_root));
    }

    private sealed class MarkerPlugin;

    /// <summary>IPluginDataStore.GetPath: raw file access for a plugin, rooted at Plugins/{assembly name}.</summary>
    [Theory]
    [InlineData("..", "Other", "secret.json")]
    [InlineData("../Other/secret.json")]
    [InlineData("cache", "..", "..", "Other", "secret.json")]
    public void Plugin_data_store_refuses_a_path_into_another_plugins_folder(params string[] segments)
    {
        var store = new PluginDataStore<MarkerPlugin>();
        try
        {
            Assert.Throws<InvalidOperationException>(() => store.GetPath(segments));
            Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(store.RootPath)!, "Other")));
        }
        finally
        {
            // RootPath is created on first access, under this test project's own build output
            if (Directory.Exists(store.RootPath) && !Directory.EnumerateFileSystemEntries(store.RootPath).Any())
            {
                Directory.Delete(store.RootPath);
            }
        }
    }

    [Theory]
    [InlineData("Stats", "Stats")]
    [InlineData("../Stats", "Stats")]
    [InlineData("..", "Unknown")]
    [InlineData("a/b", "ab")]
    public void Plugin_folder_key_cannot_contain_a_path_separator_or_parent_token(string assemblyName, string expected)
    {
        Assert.Equal(expected, PluginDirectoryResolver.Sanitize(assemblyName));
    }
}
