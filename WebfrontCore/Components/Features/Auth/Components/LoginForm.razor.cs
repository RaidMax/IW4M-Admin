using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Auth.Components;

public partial class LoginForm
{
    [Inject] public required IJSRuntime JS { get; set; }
    [Inject] public required AppState AppState { get; set; }
    private int ClientId { get; set; }
    private string Password { get; set; }
    private string ErrorMessage { get; set; }

    private async Task Login()
    {
        ErrorMessage = null;
        if (ClientId == 0 || string.IsNullOrEmpty(Password))
        {
            ErrorMessage = "Please enter ID and Password";
            return;
        }

        var request = new
        {
            ClientId,
            Password
        };

        // Call JS to perform the fetch and reload
        var result = await JS.InvokeAsync<string>("processLoginPost", "/Account/Login", request);

        if (result != "OK")
        {
            ErrorMessage = result;
        }
    }
}
