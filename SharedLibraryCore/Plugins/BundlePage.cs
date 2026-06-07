namespace SharedLibraryCore.Plugins;

/// <summary>
/// A navbar page declared in a bundle manifest. Advisory in phase 1 — plugins still self-register their
/// pages at load time; the manifest entry documents intent and supports future host-driven registration.
/// </summary>
public class BundlePage
{
    public string Name { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Permission { get; set; }
}
