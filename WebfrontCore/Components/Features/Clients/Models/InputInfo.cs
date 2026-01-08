namespace WebfrontCore.Components.Features.Clients.Models;

public class InputInfo
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Placeholder { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public string? Value { get; set; }
    public Dictionary<string, string> Values { get; set; } = [];
    public bool Checked { get; set; }
    public bool Required { get; set; }
}
