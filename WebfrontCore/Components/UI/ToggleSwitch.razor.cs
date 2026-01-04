using Microsoft.AspNetCore.Components;

namespace WebfrontCore.Components.UI;

public partial class ToggleSwitch
{
    [Parameter] public string? Id { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public bool Value { get; set; }
    [Parameter] public EventCallback<bool> ValueChanged { get; set; }

    private async Task OnValueChange(ChangeEventArgs e)
    {
        Value = (bool)(e.Value ?? false);
        await ValueChanged.InvokeAsync(Value);
    }
}