namespace WebfrontCore.Components.Features.Console.Models;

public class CommandInfo
{
    public required string Name { get; set; }
    public required string Alias { get; set; }
    public required string Description { get; set; }
    public required string Syntax { get; set; }
    public bool RequiresTarget { get; set; }
    public Data.Models.Client.EFClient.Permission Permission { get; set; }
    public required SharedLibraryCore.Server.Game[] SupportedGames { get; set; }
}
