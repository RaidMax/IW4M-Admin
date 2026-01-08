namespace WebfrontCore.Components.UI.Navigation.Models;

public interface INavigationInteractionInfo
{
    string InteractionId { get; set; }
    int MinimumPermission { get; set; }
    string Name { get; set; }
    string DisplayMeta { get; set; }
}

public class NavigationInteractionInfo : INavigationInteractionInfo
{
    public required string InteractionId { get; set; }
    public int MinimumPermission { get; set; }
    public required string Name { get; set; }
    public required string DisplayMeta { get; set; }
}
