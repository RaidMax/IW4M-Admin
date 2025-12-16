using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Admin.Pages;

public partial class Console
{
    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IToastService ToastService { get; set; }
    private List<ServerInfo> Servers { get; set; } = [];
    private string SelectedServerId { get; set; }
    private string Command { get; set; }
    private List<string> CommandOutput { get; set; } = [];
    private bool IsExecuting { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (AppState.User is null)
        {
            await ToastService.ShowErrorAsync(AppState.Loc("WEBFRONT_chain_error_permission"), AppState.Loc("WEBFRONT_modal_title_error"));
            return;
        }

        Servers = await Api.GetServersAsync();
        if (Servers.Any())
        {
            SelectedServerId = Servers.First().Id;
        }
    }

    private async Task ExecuteCommand()
    {
        if (string.IsNullOrWhiteSpace(Command) || string.IsNullOrEmpty(SelectedServerId)) return;

        IsExecuting = true;
        try
        {
            CommandOutput.Add($"> {Command}");
            var responses = await Api.ExecuteConsoleCommandAsync(SelectedServerId, Command);

            foreach (var response in responses)
            {
                CommandOutput.Add(response.Response);
            }

            Command = string.Empty;
        }
        catch (Exception ex)
        {
            CommandOutput.Add($"Error: {ex.Message}");
            await ToastService.ShowErrorAsync(ex.Message, "Command Failed");
        }
        finally
        {
            IsExecuting = false;
        }

        // Scroll to bottom
        // await JS.InvokeVoidAsync("scrollToBottom", "console_command_response");
    }
}
