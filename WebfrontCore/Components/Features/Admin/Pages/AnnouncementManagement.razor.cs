using WebfrontCore.Components.Features.Admin.Models;

namespace WebfrontCore.Components.Features.Admin.Pages;

public partial class AnnouncementManagement
{
    private IEnumerable<AnnouncementInfo>? _announcements;
    private bool _showForm;
    private int? _editingId;
    private string _formTitle = "";
    private string _formContent = "";
    private DateTime? _formStartAt;
    private DateTime? _formEndAt;
    private bool _formIsGlobal;
    private bool _formIsActive;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadAnnouncements();
    }

    private async Task LoadAnnouncements()
    {
        _announcements = await DataService.GetAllAnnouncementsAsync();
    }

    private void ShowCreateForm()
    {
        _editingId = null;
        _formTitle = "";
        _formContent = "";
        _formStartAt = null;
        _formEndAt = null;
        _formIsGlobal = false;
        _formIsActive = false;
        _errorMessage = null;
        _showForm = true;
    }

    private void EditAnnouncement(AnnouncementInfo announcement)
    {
        _editingId = announcement.AnnouncementId;
        _formTitle = announcement.Title;
        _formContent = announcement.Content;
        _formStartAt = announcement.StartAt;
        _formEndAt = announcement.EndAt;
        _formIsGlobal = announcement.IsGlobalNotice;
        _formIsActive = announcement.IsActive;
        _errorMessage = null;
        _showForm = true;
    }

    private void CancelForm()
    {
        _showForm = false;
        _editingId = null;
        _errorMessage = null;
    }

    private async Task SaveAnnouncement()
    {
        if (string.IsNullOrWhiteSpace(_formTitle))
        {
            _errorMessage = Loc["WEBFRONT_VALIDATION_REQUIRED"];
            return;
        }

        try
        {
            if (_editingId.HasValue)
            {
                await DataService.UpdateAnnouncementAsync(new UpdateAnnouncementRequest
                {
                    AnnouncementId = _editingId.Value,
                    Title = _formTitle,
                    Content = _formContent,
                    StartAt = _formStartAt,
                    EndAt = _formEndAt,
                    IsGlobalNotice = _formIsGlobal,
                    IsActive = _formIsActive
                });
            }
            else
            {
                await DataService.CreateAnnouncementAsync(new CreateAnnouncementRequest
                {
                    Title = _formTitle,
                    Content = _formContent,
                    StartAt = _formStartAt,
                    EndAt = _formEndAt,
                    IsGlobalNotice = _formIsGlobal,
                    IsActive = _formIsActive
                }, AppState.User?.ClientId ?? 0);
            }

            CancelForm();
            await LoadAnnouncements();
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
    }

    private async Task ActivateAnnouncement(int id)
    {
        await DataService.ActivateAnnouncementAsync(id);
        await LoadAnnouncements();
    }

    private async Task DeactivateAnnouncement(int id)
    {
        await DataService.DeactivateAnnouncementAsync(id);
        await LoadAnnouncements();
    }

    private async Task DeleteAnnouncement(int id)
    {
        await DataService.DeleteAnnouncementAsync(id);
        await LoadAnnouncements();
    }
}