using Microsoft.AspNetCore.Components;
using WebfrontCore.Services;
using WebfrontCore.ViewModels;

namespace WebfrontCore.Components.Pages.Configuration;

public partial class Files
{
    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IToastService ToastService { get; set; }
    private List<ConfigurationFileInfo> ConfigurationFiles { get; set; }
    private HashSet<string> ExpandedFiles { get; set; } = new();
    private Dictionary<string, string> SaveStatus { get; set; } = new();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var files = await Api.GetConfigurationFilesAsync();
            ConfigurationFiles = files.ToList();
            foreach (var f in ConfigurationFiles) SaveStatus[f.FileName] = "";
        }
        catch (Exception)
        {
            // Handle error (e.g. unauthorized)
        }
    }

    private void ToggleFile(ConfigurationFileInfo file)
    {
        if (ExpandedFiles.Contains(file.FileName))
        {
            ExpandedFiles.Remove(file.FileName);
        }
        else
        {
            ExpandedFiles.Add(file.FileName);
        }
    }

    private async Task SaveFile(ConfigurationFileInfo file)
    {
        try
        {
            SaveStatus[file.FileName] = "Saving...";
            await Api.SaveConfigurationFileAsync(file.FileName, file.FileContent);
            SaveStatus[file.FileName] = "Saved!";
            await ToastService.ShowSuccessAsync($"Saved {file.FileName}");
            await Task.Delay(3000);
            SaveStatus[file.FileName] = "";
        }
        catch (Exception ex)
        {
            SaveStatus[file.FileName] = $"Error: {ex.Message}";
            await ToastService.ShowErrorAsync(ex.Message, "Save Failed");
        }
    }
}
