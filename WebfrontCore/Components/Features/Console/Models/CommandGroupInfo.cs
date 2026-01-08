namespace WebfrontCore.Components.Features.Console.Models;

public class CommandGroupInfo
{
    public required string Name { get; set; }
    public required List<CommandInfo> Commands { get; set; }
}
