using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Dtos.Meta.Responses;

namespace WebfrontCore.Components.Pages.Client.Meta;

public partial class AdministeredPenalty
{
    [Parameter] public AdministeredPenaltyResponse Meta { get; set; }
    [Parameter] public bool ShowHeader { get; set; } = true;
    [Inject] public Services.IWebfrontApiClient Api { get; set; }
    private bool IsOpen { get; set; }
    private bool IsLoading { get; set; }
    private List<Dictionary<string, string>> SnapshotInfo { get; set; }

    private async Task ToggleDetails()
    {
        IsOpen = !IsOpen;
        if (IsOpen && SnapshotInfo == null)
        {
            IsLoading = true;
            try
            {
                SnapshotInfo = await Api.GetAutomatedPenaltyInfoAsync(Meta.PenaltyId);
            }
            catch (Exception)
            {
                // Handle error
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
