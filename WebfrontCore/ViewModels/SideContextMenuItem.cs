using System.Collections.Generic;

namespace WebfrontCore.ViewModels;

public class SideContextMenuItem
{
    public bool IsLink { get; set; }
    public bool IsButton { get; set; }
    public bool IsActive { get; set; }
    public bool IsCollapse { get; set; }
    public string Title { get; set; }
    public string Reference { get; set; }
    public string Icon { get; set; }
    public string Tooltip { get; set; }
    public int? EntityId { get; set; }
    public string Meta { get; set; }
}


public class SideContextMenuItems
{
    public string MenuTitle { get; set; }
    public List<SideContextMenuItem> Items { get; set; } = new();
}
