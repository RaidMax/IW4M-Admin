using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WebfrontCore.Components.Features.Admin.Models;

namespace WebfrontCore.Components.Features.Home;

public partial class AnnouncementBanner
{
    /// <summary>
    /// If true, only show global/sitewide announcements. If false, show any active announcement.
    /// </summary>
    [Parameter]
    public bool GlobalOnly { get; set; }

    [Parameter] public bool ExcludeGlobal { get; set; }
    private AnnouncementInfo? _announcement;
    private bool _isDismissed = true;
    private bool _allowDismiss = true;
    private string _dismissKey = "";

    protected override async Task OnInitializedAsync()
    {
        _allowDismiss = AppState.WebfrontConfig.AllowMotdDismiss;
        AnnouncementService.OnAnnouncementChanged += OnAnnouncementChanged;
        await LoadAnnouncement();
    }

    private async Task LoadAnnouncement()
    {
        _announcement = await DataService.GetActiveAnnouncementAsync(GlobalOnly);

        if (_announcement != null)
        {
            if (ExcludeGlobal && _announcement.IsGlobalNotice)
            {
                _announcement = null;
            }
            else
            {
                _dismissKey = $"motd_dismiss_{_announcement.AnnouncementId}";
            }
        }

        StateHasChanged();
    }

    private void OnAnnouncementChanged()
    {
        _ = InvokeAsync(async () =>
        {
            await LoadAnnouncement();
            // Re-check dismissal status for the new announcement if needed
            if (_announcement != null)
            {
                var isDismissedStr = await JsRuntime.InvokeAsync<string>("localStorage.getItem", _dismissKey);
                _isDismissed = !string.IsNullOrEmpty(isDismissedStr);

                StateHasChanged();
            }
        });
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _announcement != null)
        {
            var isDismissedStr = await JsRuntime.InvokeAsync<string>("localStorage.getItem", _dismissKey);
            if (string.IsNullOrEmpty(isDismissedStr))
            {
                _isDismissed = false;
                StateHasChanged();
            }
        }
    }

    private async Task Dismiss()
    {
        _isDismissed = true;
        if (_announcement != null)
        {
            await JsRuntime.InvokeVoidAsync("localStorage.setItem", _dismissKey, "true");
        }
    }

    public void Dispose()
    {
        AnnouncementService.OnAnnouncementChanged -= OnAnnouncementChanged;
    }
}