using WebCommon.Services;

namespace WebCommon.Components.UI.Controls;

public partial class ToastContainer
{
    private readonly List<ToastMessage> _messages = [];

    protected override void OnInitialized()
    {
        ToastService.OnShow += ShowToast;
    }

    private void ShowToast(ToastMessage message)
    {
        _messages.Add(message);
        InvokeAsync(StateHasChanged);
    }

    private void Remove(ToastMessage message)
    {
        _messages.Remove(message);
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        ToastService.OnShow -= ShowToast;
    }
}
