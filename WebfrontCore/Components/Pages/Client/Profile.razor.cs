using Data.Models;
using Data.Models.Client;
using Microsoft.AspNetCore.Components;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Permissions;
using WebfrontCore.Services;
using WebfrontCore.ViewModels;
using WebfrontCore.Components.Shared;

namespace WebfrontCore.Components.Pages.Client;

public partial class Profile
{
    [Parameter] public int Id { get; set; }

    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }
    [Inject] public required SharedLibraryCore.Configuration.ApplicationConfiguration Config { get; set; }
    [Inject] public required IActionService ActionService { get; set; }

    private PlayerInfo Client { get; set; }
    private SideContextMenuItems ContextItems { get; set; }
    private bool _isLoading = true;
    private string _error = null;
    private MetaType? _selectedMetaFilter = null;
    private bool IsAuthorized => AppState.User != null && (int)AppState.User.Level >= (int)EFClient.Permission.Trusted;

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        _error = null;
        _selectedMetaFilter = null;

        try
        {
            Client = await Api.GetClientProfileAsync(Id);
            if (Client != null)
            {
                BuildContextMenu();
            }
        }
        catch (Exception ex)
        {
            _error = ex.Message;
            System.Console.WriteLine($"Profile load error: {ex}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void SetMetaFilter(MetaType type)
    {
        _selectedMetaFilter = type == MetaType.All ? null : type;
        StateHasChanged();
    }

    private IEnumerable<MetaType> GetFilterableMetaTypes()
    {
        var ignoredTypes = new[] { MetaType.Information, MetaType.Other, MetaType.QuickMessage };
        return Enum.GetValues<MetaType>()
            .Where(meta => !ignoredTypes.Contains(meta))
            .OrderByDescending(meta => meta == MetaType.All);
    }

    private static string GetShortCode(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "?";
        var match = System.Text.RegularExpressions.Regex.Match(name.ToUpper(), "[A-Z]").Value;
        return string.IsNullOrEmpty(match) ? "?" : match;
    }

    private void BuildContextMenu()
    {
        var isFlagged = Client.LevelInt == (int)EFClient.Permission.Flagged;
        var isPermBanned = Client.LevelInt == (int)EFClient.Permission.Banned;
        var isTempBanned = Client.ActivePenalty?.Type == EFPenalty.PenaltyType.TempBan;
        var userLevel = AppState.User?.Level ?? EFClient.Permission.User;

        ContextItems = new SideContextMenuItems
        {
            MenuTitle = AppState.Loc("WEBFRONT_PROFILE_CONTEXT_MENU_TITLE")
        };

        // Join Server (if online)
        if (Client.Online && !string.IsNullOrEmpty(Client.ConnectProtocolUrl))
        {
            ContextItems.Items.Add(new SideContextMenuItem
            {
                Title = AppState.Loc("WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_JOIN"),
                IsButton = true,
                IsLink = true,
                Reference = Client.ConnectProtocolUrl,
                Tooltip = AppState.Loc("WEBFRONT_PROFILE_CONTEXT_MENU_TOOLTIP_JOIN")
                    .FormatExt(Client.CurrentServerName?.StripColors() ?? ""),
                Icon = "oi-play-circle",
            });
        }

        // Edit Level (if authorized)
        if (Client.LevelInt != -1 && IsAuthorized)
        {
            ContextItems.Items.Add(new SideContextMenuItem
            {
                Title = AppState.Loc("WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_LEVEL"),
                IsButton = true,
                Reference = "edit",
                Icon = "oi-cog",
                EntityId = Client.ClientId
            });
        }

        // Set Tag (if authorized)
        if (IsAuthorized)
        {
            ContextItems.Items.Add(new SideContextMenuItem
            {
                Title = AppState.Loc("WEBFRONT_PROFILE_CONTEXT_MENU_TAG"),
                IsButton = true,
                Reference = "SetClientTag",
                Icon = "oi-tag",
                EntityId = Client.ClientId
            });
        }

        // Add Note (if authorized + permission)
        if (IsAuthorized && HasPermission(WebfrontEntity.ClientNote, WebfrontPermission.Write))
        {
            ContextItems.Items.Add(new SideContextMenuItem
            {
                Title = AppState.Loc("WEBFRONT_PROFILE_CONTEXT_MENU_NOTE"),
                IsButton = true,
                Reference = "AddClientNote",
                Icon = "oi-clipboard",
                EntityId = Client.ClientId
            });
        }

        // Send Message (if authorized)
        if (IsAuthorized)
        {
            ContextItems.Items.Add(new SideContextMenuItem
            {
                Title = AppState.Loc("WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_MESSAGE"),
                IsButton = true,
                Reference = "OfflineMessage",
                Icon = "oi oi-envelope-closed",
                EntityId = Client.ClientId
            });
        }

        // View Stats (always)
        ContextItems.Items.Add(new SideContextMenuItem
        {
            Title = AppState.Loc("WEBFRONT_PROFILE_CONTEXT_MENU_ACTION_STATS"),
            IsButton = true,
            IsLink = true,
            Reference = $"/Client/Statistics/{Id}/Advanced",
            Icon = "oi-graph",
        });

        // Flag/Unflag (if authorized + not perm banned)
        if (!isPermBanned && IsAuthorized)
        {
            ContextItems.Items.Add(new SideContextMenuItem
            {
                Title = isFlagged
                    ? AppState.Loc("WEBFRONT_ACTION_UNFLAG_NAME")
                    : AppState.Loc("WEBFRONT_ACTION_FLAG_NAME"),
                IsButton = true,
                Reference = isFlagged ? "unflag" : "flag",
                Icon = "oi-flag",
                EntityId = Client.ClientId
            });
        }

        // Kick (if online + higher level)
        if (Client.LevelInt < (int)userLevel && Client.Online)
        {
            ContextItems.Items.Add(new SideContextMenuItem
            {
                Title = AppState.Loc("WEBFRONT_ACTION_KICK_NAME"),
                IsButton = true,
                Reference = "kick",
                Icon = "oi-circle-x",
                EntityId = Client.ClientId
            });
        }

        // Ban (if not banned + higher level + authorized)
        if ((Client.LevelInt < (int)userLevel && !isPermBanned || isTempBanned) && IsAuthorized)
        {
            ContextItems.Items.Add(new SideContextMenuItem
            {
                Title = AppState.Loc("WEBFRONT_ACTION_BAN_NAME"),
                IsButton = true,
                Reference = "ban",
                Icon = "oi-lock-unlocked",
                EntityId = Client.ClientId
            });
        }

        // Unban (if banned + higher level + authorized)
        if ((Client.LevelInt < (int)userLevel && isPermBanned || isTempBanned) && IsAuthorized)
        {
            ContextItems.Items.Add(new SideContextMenuItem
            {
                Title = AppState.Loc("WEBFRONT_ACTION_UNBAN_NAME"),
                IsButton = true,
                Reference = "unban",
                Icon = "oi-lock-locked",
                EntityId = Client.ClientId
            });
        }

        // Plugin Interactions
        if (Client.Interactions == null)
        {
            return;
        }

        foreach (var interaction in Client.Interactions.Where(i =>
                     (int)userLevel >= ((int?)i.MinimumPermission ?? 0)))
        {
            ContextItems.Items.Add(new SideContextMenuItem
            {
                Title = interaction.Name,
                Tooltip = interaction.Description,
                EntityId = interaction.EntityId,
                Icon = interaction.DisplayMeta,
                Reference = interaction.ActionPath,
                Meta = System.Text.Json.JsonSerializer.Serialize(interaction.ActionMeta),
                IsButton = true
            });
        }
    }

    private bool HasPermission(WebfrontEntity entity, WebfrontPermission permission)
    {
        return AppState.User != null && Config.HasPermission(AppState.User.Level, entity, permission);
    }

    private string ClassForProfileBackground()
    {
        return !HasPermission(WebfrontEntity.ClientLevel, WebfrontPermission.Read)
            ? "level-bgcolor-0"
            : $"level-bgcolor-{Client.LevelInt}";
    }

    private static string ClassForPenaltyType(EFPenalty.PenaltyType type)
    {
        return type switch
        {
            EFPenalty.PenaltyType.Ban => "alert-danger",
            EFPenalty.PenaltyType.Flag => "alert-secondary",
            EFPenalty.PenaltyType.TempBan => "alert-secondary",
            EFPenalty.PenaltyType.TempMute => "alert-secondary",
            EFPenalty.PenaltyType.Mute => "alert-secondary",
            _ => "alert"
        };
    }

    private void OnProfileContextAction(SideContextMenuItem item)
    {
        ActionService.OpenAction(item.Reference, item.EntityId, item.Meta);
    }
}
