using Data.Models;
using Data.Models.Client;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WebfrontCore.Services;
using WebfrontCore.ViewModels;

namespace WebfrontCore.Components.Shared;

public partial class ActionModal
{
    [Inject] public required NavigationManager Nav { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IZeroJsInterop Js { get; set; }
    [Inject] public required IJSRuntime Runtime { get; set; }
    [Inject] public required IActionService ActionService { get; set; }
    private bool _isLoading;
    private bool _isLegacy;
    private bool _isLogin;
    private string _error;
    private ActionInfo _actionInfo;
    private readonly Dictionary<string, object?> _formData = new();
    private int? _targetId;

    [Parameter] public string ModalId { get; set; } = "action-modal";

    public async Task Open(string actionName, int? targetId = null, string meta = null)
    {
        _isLoading = true;
        _error = null;
        _actionInfo = null;
        _targetId = targetId;
        _formData.Clear();

        // Login is handled by specific component
        _isLogin = actionName.Equals("Login", StringComparison.OrdinalIgnoreCase);
        _isLegacy = false;

        // Ensure modal is shown (toggle if hidden)
        await Runtime.InvokeVoidAsync("halfmoon.toggleModal", ModalId);

        if (_isLogin)
        {
            _isLoading = false;
            StateHasChanged();
            return;
        }

        StateHasChanged();

        try
        {
            _actionInfo = await ActionService.GetActionInfoAsync(actionName, targetId, meta);

            if (_actionInfo != null)
            {
                // Init form data with defaults
                foreach (var input in _actionInfo.Inputs)
                {
                    if (input.Type == "hidden")
                    {
                        _formData[input.Name] = input.Value;
                    }
                    else if (input.Values != null && input.Type == "select")
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
                        _formData[input.Name] = input.Value;
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
            Console.WriteLine(ex);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task Close()
    {
        await Runtime.InvokeVoidAsync("halfmoon.toggleModal", ModalId);
        _actionInfo = null;
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

            var (success, message) = await ActionService.ExecuteActionAsync(_actionInfo.Action, _targetId, _formData, origin);

            // Show toast/alert
            string alertType;
            string title;

            if (!success)
            {
                alertType = "alert-danger";
                title = AppState.Loc("WEBFRONT_SCRIPT_ACTION_ERROR");
            }
            else
            {
                alertType = _actionInfo.ShouldRefresh ? "alert-success" : "alert-primary";
                title = _actionInfo.ShouldRefresh
                    ? AppState.Loc("WEBFRONT_SCRIPT_ACTION_SUCCESS")
                    : AppState.Loc("WEBFRONT_SCRIPT_ACTION_EXECUTED");
            }

            // Fix Halfmoon stickyAlerts reference if missing
            try
            {
                await Runtime.InvokeVoidAsync("eval",
                    "if(typeof halfmoon !== 'undefined' && !halfmoon.stickyAlerts) { halfmoon.stickyAlerts = document.getElementsByClassName('sticky-alerts')[0]; }");
            }
            catch
            {
            }

            await Js.InitStickyAlert(title, message, alertType);

            if (success && _actionInfo.ShouldRefresh)
            {
                await Task.Delay(2000);
                Nav.NavigateTo(Nav.Uri, forceLoad: true);
            }

            await Close();
        }
        catch (Exception ex)
        {
            await Js.InitStickyAlert(AppState.Loc("WEBFRONT_SCRIPT_ACTION_ERROR"), ex.Message, "alert-danger");
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
