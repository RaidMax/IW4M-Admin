namespace WebfrontCore.Controllers.API.Models;

public class InteractionResponse
{
    public required string Title { get; set; }
    public required string Content { get; set; }
    public required string InteractionType { get; set; }
    public required string DisplayMeta { get; set; }
}
