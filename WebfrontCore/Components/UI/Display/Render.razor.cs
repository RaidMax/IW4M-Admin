using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WebfrontCore.Core.Services;
using WebCommon.Services;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Controllers.API.Models;

namespace WebfrontCore.Components.UI.Display;

public partial class Render : IAsyncDisposable
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }
    [Inject] public required IActionService ActionService { get; set; }
    [Inject] public required IJSRuntime JsRuntime { get; set; }
    [Inject] public required IToastService ToastService { get; set; }
    
    [Parameter, EditorRequired] public string InteractionName { get; set; } = default!;
    private InteractionResponse? InteractionData;
    protected InteractionType ParsedInteractionType;
    private bool IsLoading = true;
    private string? ErrorMessage;
    private DotNetObjectReference<Render>? _dotNetRef;

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
            if (InteractionData != null)
            {
                Enum.TryParse(InteractionData.InteractionType, out ParsedInteractionType);
            }
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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!IsLoading && InteractionData != null)
        {
            _dotNetRef ??= DotNetObjectReference.Create(this);
            await JsRuntime.InvokeVoidAsync("setupDynamicActionHandlers", _dotNetRef);
        }
    }

    [JSInvokable]
    public void HandleDynamicAction(string? action, int? actionId, string actionMeta)
    {
        if (action?.Equals("DynamicAction", StringComparison.OrdinalIgnoreCase) == true)
        {
            ActionService.OpenAction("DynamicAction", actionId, actionMeta);
        }
        else if (!string.IsNullOrEmpty(action))
        {
            ActionService.OpenAction(action, actionId, actionMeta);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dotNetRef?.Dispose();
    }
}
