using Microsoft.AspNetCore.Components;
using WebfrontCore.Core.Services;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Controllers.API.Models;

namespace WebfrontCore.Components.UI.Display;

public partial class Render
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }
    [Parameter] public string InteractionName { get; set; }
    private InteractionResponse InteractionData;
    protected InteractionType ParsedInteractionType;
    private bool IsLoading = true;
    private string ErrorMessage;

    protected override async Task OnParametersSetAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        InteractionData = null;

        try
        {
            var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query)
                .ToDictionary(k => k.Key, v => v.Value.ToString());
            InteractionData = await DataService.GetInteractionAsync(InteractionName, query);
            Enum.TryParse(InteractionData.InteractionType, out ParsedInteractionType);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            ErrorMessage = AppState.Loc("WEBFRONT_ERROR_GENERIC");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            ErrorMessage = AppState.Loc("WEBFRONT_ACTION_UNAUTHORIZED");
        }
        catch (Exception)
        {
            ErrorMessage = AppState.Loc("WEBFRONT_ERROR_GENERIC");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
