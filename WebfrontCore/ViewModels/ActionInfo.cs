namespace WebfrontCore.ViewModels;

public class ActionInfo
{
    public string Name { get; set; }
    public List<InputInfo> Inputs { get; set; }
    public string ActionButtonLabel { get; set; }
    public string Action { get; set; }
    public bool ShouldRefresh { get; set; }
}
