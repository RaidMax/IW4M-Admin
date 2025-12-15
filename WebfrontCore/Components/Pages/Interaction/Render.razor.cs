using Microsoft.AspNetCore.Components;
using WebfrontCore.Controllers.API;
using WebfrontCore.Services;

namespace WebfrontCore.Components.Pages.Interaction;

public partial class Render
{
    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Parameter] public string InteractionName { get; set; }
    private InteractionResponse InteractionData;
    private bool IsLoading = true;
    private string ErrorMessage;

    protected override async Task OnParametersSetAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        InteractionData = null;

        try
        {
            InteractionData = await Api.GetInteractionAsync(InteractionName);
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
