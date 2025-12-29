using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Admin.Pages;

public partial class Console
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IToastService ToastService { get; set; }
    
    private List<ServerInfo> Servers { get; set; } = [];
    private string SelectedServerId { get; set; }
    private string Command { get; set; }
    private List<string> CommandOutput { get; set; } = [];
    private bool IsExecuting { get; set; }
    
    private List<string> CommandHistory { get; set; } = [];
    private int HistoryIndex { get; set; } = -1;
    private ElementReference ScrollAnchor { get; set; }
    private ElementReference InputElement { get; set; }
    private int _lastOutputCount;
    private bool _initialized;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Load servers on first render after the interactive circuit is established
            Servers = await DataService.GetServersAsync();
            if (Servers.Any())
            {
                SelectedServerId = Servers.First().Id;
            }
            
            _initialized = true;
            StateHasChanged();
        }
        
        if (_initialized && CommandOutput.Count > _lastOutputCount)
        {
            _lastOutputCount = CommandOutput.Count;
            await ScrollToBottomAsync();
        }
    }
    
    private async Task ScrollToBottomAsync()
    {
        try
        {
            await ScrollAnchor.FocusAsync();
            await InputElement.FocusAsync();
        }
        catch
        {
            // Ignore focus errors
        }
    }

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "ArrowUp")
        {
            if (CommandHistory.Count == 0) return;
            
            HistoryIndex = Math.Min(HistoryIndex + 1, CommandHistory.Count - 1);
            if (HistoryIndex >= 0)
            {
                Command = CommandHistory[CommandHistory.Count - 1 - HistoryIndex];
            }
        }
        else if (e.Key == "ArrowDown")
        {
            if (HistoryIndex == -1) return;
            
            HistoryIndex = Math.Max(HistoryIndex - 1, -1);
            if (HistoryIndex == -1)
            {
                Command = string.Empty;
            }
            else
            {
                Command = CommandHistory[CommandHistory.Count - 1 - HistoryIndex];
            }
        }
    }

    private async Task ExecuteCommand()
    {
        if (string.IsNullOrWhiteSpace(Command) || string.IsNullOrEmpty(SelectedServerId)) return;

        // Add to history if unique or last command is different
        if (!CommandHistory.Any() || CommandHistory.Last() != Command)
        {
            CommandHistory.Add(Command);
        }
        HistoryIndex = -1;

        IsExecuting = true;
        var commandToExecute = Command;
        Command = string.Empty;
        
        try
        {
            CommandOutput.Add($"> {commandToExecute}");
            var responses = await DataService.ExecuteCommandAsync(SelectedServerId, commandToExecute);

            foreach (var response in responses)
            {
                CommandOutput.Add(response.Response);
            }
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
        
        StateHasChanged();
    }
}
