using Data.Models.Client.Stats;
using Data.Models.Client.Stats.Reference;
using Microsoft.AspNetCore.Components;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using Stats.Dtos;
using Stats.Helpers;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Clients.Statistics;

public partial class AdvancedStats
{
    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }
    [Inject] public required DefaultSettings DefaultConfig { get; set; }
    [Inject] public required IZeroJsInterop JsInterop { get; set; }

    [Parameter] public int ClientId { get; set; }
    [SupplyParameterFromQuery] public string serverId { get; set; }

    private AdvancedStatsInfo Stats;
    private SideContextMenuItems MenuItems;
    private object HitLocationData;
    private float MaxPercentage;
    private object HistoryData;
    private bool _chartsInitialized = false;

    protected override async Task OnParametersSetAsync()
    {
        _chartsInitialized = false; // Reset when params change
        try
        {
            Stats = await Api.GetAdvancedStatsAsync(ClientId, serverId);
            GenerateMenu();
        }
        catch (Exception ex)
        {
            NavManager.NavigateTo("/client" + ClientId);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Stats != null && HitLocationData != null && !_chartsInitialized)
        {
            _chartsInitialized = true;
            // Wait for DOM to be fully rendered before initializing charts
            await Task.Delay(200);
            try
            {
                await JsInterop.InitAdvancedStats(HistoryData, HitLocationData, MaxPercentage);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error calling initAdvancedStats: {ex.Message}");
            }
        }
    }

    private void GenerateMenu()
    {
        MenuItems = new SideContextMenuItems
        {
            MenuTitle = AppState.Loc("WEBFRONT_CONTEXT_MENU_GLOBAL_GAME"),
            Items = Stats.Servers.Select(server => new SideContextMenuItem
            {
                IsLink = true,
                Reference = $"/client/{ClientId}/stats?serverId={server.Endpoint}",
                Title = server.Name.StripColors(),
                IsActive = Stats.ServerEndpoint == server.Endpoint,
                Meta = server.Game.ToString(),
                IsCollapse = true
            }).Prepend(new SideContextMenuItem
            {
                IsLink = true,
                Reference = $"/client/{ClientId}/stats",
                Title = AppState.Loc("WEBFRONT_STATS_INDEX_ALL_SERVERS"),
                IsActive = Stats.ServerEndpoint == null
            }).ToList()
        };
    }


    private string GetWeaponNameForHit(EFClientHitStatistic stat, GameStringConfiguration config)
    {
        if (stat == null)
            return null;
        var rebuiltName = stat.RebuildWeaponName();
        var name = config.GetStringForGame(rebuiltName, stat.Weapon?.Game);
        return !rebuiltName.Equals(name, StringComparison.InvariantCultureIgnoreCase)
            ? name
            : config.GetStringForGame(stat.Weapon.Name, stat.Weapon.Game);
    }

    private string GetWeaponAttachmentName(EFWeaponAttachmentCombo attachment, GameStringConfiguration config)
    {
        if (attachment == null)
            return null;
        var attachmentText = string.Join(" + ", new[]
        {
            config.GetStringForGame(attachment.Attachment1.Name, attachment.Attachment1.Game),
            config.GetStringForGame(attachment.Attachment2?.Name, attachment.Attachment2?.Game),
            config.GetStringForGame(attachment.Attachment3?.Name, attachment.Attachment3?.Game)
        }.Where(attach => !string.IsNullOrWhiteSpace(attach)));

        return attachmentText;
    }

    private int GetRankIconIndex(double? zScore)
    {
        // Logic from IW4MAdmin.Plugins.Stats.Extensions.RankIconIndexForZScore
        if (zScore == null)
        {
            return 0;
        }

        const int ZScoreRange = 3;
        const int RankIconDivisions = 24;
        const double divisionIncrement = (ZScoreRange * 2) / (double)RankIconDivisions;
        var rank = 1;

        for (var i = rank; i <= RankIconDivisions; i++)
        {
            var bottom = Math.Round(-ZScoreRange + (i - 1) * divisionIncrement, 5);
            var top = Math.Round(-ZScoreRange + i * divisionIncrement, 5);

            if (zScore > bottom && zScore <= top)
            {
                return rank;
            }

            if (i == 1 && zScore < bottom // catch all for really bad players
                // catch all for very good players
                || i == RankIconDivisions && zScore > top)
            {
                return i;
            }

            rank++;
        }

        return 0;
    }
}
