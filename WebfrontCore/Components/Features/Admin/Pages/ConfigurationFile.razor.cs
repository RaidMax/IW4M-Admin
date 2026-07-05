using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Configuration;
using WebfrontCore.Core.Services;
using WebCommon.Services;
using WebfrontCore.Components.Features.Admin.Models;

namespace WebfrontCore.Components.Features.Admin.Pages;

public partial class ConfigurationFile
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IToastService ToastService { get; set; }
    private List<ConfigurationFileInfo> ConfigurationFiles { get; set; } = [];
    private HashSet<string> ExpandedFiles { get; set; } = new();
    private Dictionary<string, string> SaveStatus { get; set; } = new();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var files = await DataService.GetConfigurationFilesAsync();
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
            await DataService.SaveConfigurationFileAsync(file.FileName, file.FileContent);
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
