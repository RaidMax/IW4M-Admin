namespace WebfrontCore.Controllers.API.Models;

public class CommandRequest
{
    public required string ServerId { get; set; }
    public required string Command { get; set; }
}
