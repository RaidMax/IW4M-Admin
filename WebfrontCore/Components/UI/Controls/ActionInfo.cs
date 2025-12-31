using WebfrontCore.Components.Features.Clients.Models;

namespace WebfrontCore.Components.UI.Controls;

public class ActionInfo
{
    public required string Name { get; set; }
    public List<InputInfo> Inputs { get; set; } = [];
    public required string ActionButtonLabel { get; set; }
    public required string Action { get; set; }
    public bool ShouldRefresh { get; set; }
}
