using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Clients.Components;

public partial class ClientInformationTags
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }

    [Parameter] public int ClientId { get; set; }

    private bool _isLoading = true;
    private IEnumerable<IGrouping<string, InformationResponse>> _groupedMeta = [];
    private int _previousClientId;

    // Define category order for consistent display
    private static readonly string[] CategoryOrder = ["General", "Statistics", "AntiCheat"];

    protected override async Task OnParametersSetAsync()
    {
        if (_previousClientId != ClientId)
        {
            _previousClientId = ClientId;
            await LoadMetaAsync();
        }
    }

    private async Task LoadMetaAsync()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            var meta = await DataService.GetClientMetaAsync(new Controllers.API.Models.ClientMetaRequest
            {
                ClientId = ClientId,
                Count = 100,
                MetaType = MetaType.Information
            });

            var infoMeta = meta.OfType<InformationResponse>().ToList();

            // Group by category and order by predefined category order
            _groupedMeta = infoMeta
                .GroupBy(m => m.Category ?? "General")
                .OrderBy(g => Array.IndexOf(CategoryOrder, g.Key) is var idx && idx >= 0 ? idx : 999);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error loading client meta: {ex.Message}");
            _groupedMeta = [];
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static (string Icon, string Title, string AccentClass) GetCategoryDisplay(string category)
    {
        return category switch
        {
            "General" => ("ph-info", "General Information", "text-primary"),
            "Statistics" => ("ph-chart-bar", "Game Statistics", "text-success"),
            "AntiCheat" => ("ph-shield-check", "AntiCheat Metrics", "text-warning"),
            _ => ("ph-tag", category, "bg-gradient-to-br from-gray-500 to-gray-600")
        };
    }

    private static string GetItemIcon(string key)
    {
        var lowerKey = key.ToLower();
        return lowerKey switch
        {
            var k when k.Contains("map") => "ph-map-trifold",
            var k when k.Contains("server") => "ph-hard-drives",
            var k when k.Contains("time") || k.Contains("play") => "ph-clock",
            var k when k.Contains("first") => "ph-calendar-plus",
            var k when k.Contains("last") || k.Contains("seen") => "ph-calendar-check",
            var k when k.Contains("connection") => "ph-plug",
            var k when k.Contains("hidden") || k.Contains("mask") => "ph-eye-slash",
            _ => "ph-info"
        };
    }
}
