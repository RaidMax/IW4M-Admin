using Data.Models;
using Data.Models.Client;
using Microsoft.AspNetCore.Components;
using WebfrontCore.Core.Services;
using WebCommon.Services;

namespace WebfrontCore.Components.UI.Controls;

public partial class ActionModal
{
    [Inject] public required NavigationManager Nav { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IActionService ActionService { get; set; }
    [Inject] public required IToastService ToastService { get; set; }
    [Inject] public required ILogger<ActionModal> Logger { get; set; }
    private bool _isLoading;
    private bool _isLegacy;
    private bool _isLogin;
    private string? _error;
    private ActionInfo? _actionInfo;
    private readonly Dictionary<string, object?> _formData = new();
    private int? _targetId;
    private string? _serverId;
    private bool _isVisible;

    [Parameter] public string ModalId { get; set; } = "action-modal";
    private RenderFragment? _childContent;
    private string? _customTitle;

    // Header text for the ModalShell: a loaded action's name, else a custom modal's title, else the
    // login title, else a generic fallback.
    private string? HeaderTitle =>
        _actionInfo?.Name
        ?? (_childContent != null ? _customTitle : (_isLogin ? AppState.Loc("WEBFRONT_NAV_TITLE_LOGIN") : "Action"));
    // Default modal sizing — the shell carries no sizing of its own, so a caller-supplied
    // modalClass is the COMPLETE size set (width AND max-height), not an addition to a baked-in
    // default it would have to !-override. Classes must exist in the host stylesheet — see the
    // modal sizing vocabulary safelisted in wwwroot/css/src/app.css.
    private const string DefaultModalClass = "max-w-lg max-h-[90vh]";
    private string _modalClass = DefaultModalClass;
    // Default body styling — padded + auto scroll, suits form actions and the
    // simple custom modals. Custom callers can override via OpenCustom's
    // bodyClass param when they need edge-to-edge / fixed-height layouts.
    private const string DefaultBodyClass = "p-6 overflow-y-auto";
    private string _bodyClass = DefaultBodyClass;

    
    protected override void OnInitialized()
    {
        base.OnInitialized();
        ActionService.OnOpenAction += OnOpenAction;
        ActionService.OnOpenCustomAction += OnOpenCustomAction;
    }

    public void Dispose()
    {
        ActionService.OnOpenAction -= OnOpenAction;
        ActionService.OnOpenCustomAction -= OnOpenCustomAction;
    }

    private async void OnOpenCustomAction(ModalRequest request)
    {
        _childContent = request.Content;
        _customTitle = request.Title;
        _modalClass = request.ModalClass ?? DefaultModalClass;
        _bodyClass = request.BodyClass ?? DefaultBodyClass;

        _actionInfo = null; // Clear standard action info
        _error = null;
        _isLoading = false;
        _isVisible = true;
        await InvokeAsync(StateHasChanged);
    }

    private async void OnOpenAction(string actionName, int? targetId, string meta, string? serverId)
    {
        _childContent = null;
        _modalClass = DefaultModalClass;
        _bodyClass = DefaultBodyClass;
        await Open(actionName, targetId, meta, serverId);
        await InvokeAsync(StateHasChanged);
    }


    public async Task Open(string actionName, int? targetId = null, string? meta = null, string? serverId = null)
    {
        _isLoading = true;
        _error = null;
        _actionInfo = null;
        _targetId = targetId;
        _serverId = serverId;
        _formData.Clear();

        // Login is handled by specific component
        _isLogin = actionName.Equals("Login", StringComparison.OrdinalIgnoreCase);
        _isLegacy = false;

        // Ensure modal is shown (toggle if hidden)
        // Ensure modal is shown (toggle if hidden)
        _isVisible = true;

        if (_isLogin)
        {
            _isLoading = false;
            StateHasChanged();
            return;
        }

        StateHasChanged();

        try
        {
            _actionInfo = await ActionService.GetActionInfoAsync(actionName, targetId, meta ?? string.Empty, serverId);

            if (_actionInfo != null)
            {
                // Init form data with defaults
                foreach (var input in _actionInfo.Inputs)
                {
                    if (input.Type == "hidden")
                    {
                        _formData[input.Name] = input.Value;
                    }
                    else if (input is { Type: "select" })
                    {
                        // Handle !selected!
                        var selected = input.Values.FirstOrDefault(k => k.Key.StartsWith("!selected!"));
                        if (!selected.Equals(default(KeyValuePair<string, string>)))
                        {
                            _formData[input.Name] = selected.Key.Replace("!selected!", "");
                        }
                        else
                        {
                            _formData[input.Name] = input.Values.FirstOrDefault().Key ?? ""; // Default to first (usually empty if Preset)
                        }
                    }
                    else if (input.Type == "checkbox")
                    {
                        _formData[input.Name] = input.Checked;
                    }
                    else
                    {
                        _formData[input.Name] = input.Value ?? string.Empty;
                    }
                }
            }
            else
            {
                _error = $"Action '{actionName}' not found.";
            }
        }
        catch (Exception ex)
        {
            _error = $"Failed to load action: {ex.Message}";
            Logger.LogError(ex, "Failed to load action");
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task Close()
    {
        _isVisible = false;
        _actionInfo = null;
        _childContent = null;
        StateHasChanged();
    }

    private async Task Submit()
    {
        // Legacy forms (Login) don't use this method, they submit natively.
        // This is for Blazor-handled actions only.

        _isLoading = true;

        try
        {
            // Create Origin generic client
            var origin = new EFClient
            {
                ClientId = AppState.User?.ClientId ?? 0,
                Level = AppState.User?.Level ?? EFClient.Permission.User,
                CurrentAlias = new EFAlias { Name = AppState.User?.Name ?? "Webfront" }
            };

            var (success, message) = await ActionService.ExecuteActionAsync(_actionInfo!.Action, _targetId, _formData!, origin, _serverId);

            if (!success)
            {
                await ToastService.ShowErrorAsync(message, AppState.Loc("WEBFRONT_SCRIPT_ACTION_ERROR"));
            }
            else
            {
                if (_actionInfo.ShouldRefresh)
                {
                    await ToastService.ShowSuccessAsync(message, AppState.Loc("WEBFRONT_SCRIPT_ACTION_SUCCESS"));
                }
                else
                {
                    await ToastService.ShowInfoAsync(message, AppState.Loc("WEBFRONT_SCRIPT_ACTION_EXECUTED"));
                }
            }

            if (success && _actionInfo.ShouldRefresh)
            {
                await Task.Delay(2000);
                Nav.NavigateTo(Nav.Uri, forceLoad: true);
            }

            await Close();
        }
        catch (Exception ex)
        {
            await ToastService.ShowErrorAsync(ex.Message, AppState.Loc("WEBFRONT_SCRIPT_ACTION_ERROR"));
            _error = ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private string ProcessColor(string value)
    {
        return System.Text.RegularExpressions.Regex.Replace(value, "\\^[0-9:a-z]", "");
    }
}
