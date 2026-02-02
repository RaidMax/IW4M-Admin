using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Auth.Components;

public partial class LoginForm
{
    [Inject] public required IJSRuntime JS { get; set; }
    [Inject] public required AppState AppState { get; set; }
    private int ClientId { get; set; }
    private string? Password { get; set; }
    private string? TwoFactorCode { get; set; }
    private bool TwoFactorRequired { get; set; }
    private string? ErrorMessage { get; set; }

    private async Task Login()
    {
        ErrorMessage = null;
        if (ClientId == 0 || string.IsNullOrEmpty(Password))
        {
            ErrorMessage = "Please enter ID and Password";
            return;
        }

        if (TwoFactorRequired && string.IsNullOrEmpty(TwoFactorCode))
        {
             ErrorMessage = "Please enter 2FA Code";
             return;
        }

        var request = new
        {
            ClientId,
            Password,
            TwoFactorCode
        };

        // Call JS to perform the fetch and reload
        var result = await JS.InvokeAsync<string>("processLoginPost", "/Account/Login", request);

        if (result == "2FA_REQUIRED")
        {
            TwoFactorRequired = true;
            ErrorMessage = null;
            StateHasChanged();
            return;
        }

        if (result != "OK")
        {
            ErrorMessage = result;
        }
    }
}
