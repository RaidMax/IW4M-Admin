namespace WebfrontCore.Components.Features.Home.Models;

public class ServerRulesInfo
{
    public required string ServerName { get; set; }
    public required string IPAddress { get; set; }
    public int Port { get; set; }
    public required string[] Rules { get; set; }
}
