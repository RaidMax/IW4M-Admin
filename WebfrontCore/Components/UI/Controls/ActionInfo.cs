using WebfrontCore.Components.Features.Clients.Models;

namespace WebfrontCore.Components.UI.Controls;

public class ActionInfo
{
    public string Name { get; set; }
    public List<InputInfo> Inputs { get; set; }
    public string ActionButtonLabel { get; set; }
    public string Action { get; set; }
    public bool ShouldRefresh { get; set; }
}
