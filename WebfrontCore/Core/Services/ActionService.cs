using System.Text.Json;
using Data.Models;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos.Meta.Responses;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Components.Features.Clients.Models;
using WebfrontCore.Components.UI.Controls;

namespace WebfrontCore.Core.Services;

public interface IActionService
{
    event Action<string, int?, string, string?> OnOpenAction;
    event Action<Microsoft.AspNetCore.Components.RenderFragment, string, string?> OnOpenCustomAction;

    void OpenAction(string actionName, int? targetId, string meta, string? serverId = null);
    void OpenCustom(Microsoft.AspNetCore.Components.RenderFragment content, string title, string? modalClass = null);

    Task<ActionInfo> GetActionInfoAsync(string actionName, int? targetId, string meta, string? serverId = null);

    Task<(bool Success, string Message)> ExecuteActionAsync(string actionName, int? targetId,
        Dictionary<string, object> formData, EFClient origin, string? serverId = null);
}

public class ActionService : IActionService
{
    private readonly IManager _manager;
    private readonly ApplicationConfiguration _appConfig;
    private readonly ITranslationLookup _localization;
    private readonly IMetaServiceV2 _metaService;
    private readonly IRemoteCommandService _remoteCommandService;
    private readonly IInteractionRegistration _interactionRegistration;
    private readonly ILogger<ActionService> _logger;

    public event Action<string, int?, string, string?> OnOpenAction = delegate { };

    public event Action<Microsoft.AspNetCore.Components.RenderFragment, string, string?> OnOpenCustomAction = delegate
    {
    };

    public void OpenAction(string actionName, int? targetId, string meta, string? serverId = null)
    {
        OnOpenAction?.Invoke(actionName, targetId, meta, serverId);
    }

    public void OpenCustom(Microsoft.AspNetCore.Components.RenderFragment content, string title,
        string? modalClass = null)
    {
        OnOpenCustomAction?.Invoke(content, title, modalClass);
    }

    // Command Names
    private readonly string? _banCommandName;
    private readonly string? _tempbanCommandName;
    private readonly string? _unbanCommandName;
    private readonly string? _kickCommandName;
    private readonly string? _flagCommandName;
    private readonly string? _unflagCommandName;
    private readonly string? _setLevelCommandName;
    private readonly string? _offlineMessageCommandName;
    private readonly string? _setClientTagCommandName;
    private readonly string? _addClientNoteCommandName;
    private readonly string? _sayCommandName;

    public ActionService(IManager manager, ApplicationConfiguration appConfig, ITranslationLookup localization,
        IMetaServiceV2 metaService, IRemoteCommandService remoteCommandService,
        IEnumerable<IManagerCommand> registeredCommands,
        IInteractionRegistration interactionRegistration, ILogger<ActionService> logger)
    {
        _manager = manager;
        _appConfig = appConfig;
        _localization = localization;
        _metaService = metaService;
        _remoteCommandService = remoteCommandService;
        _interactionRegistration = interactionRegistration;
        _logger = logger;

        // Resolve command names
        foreach (var cmd in registeredCommands)
        {
            var type = cmd.GetType().Name;

            switch (type)
            {
                case nameof(BanCommand):
                    _banCommandName = cmd.Name;
                    break;
                case nameof(TempBanCommand):
                    _tempbanCommandName = cmd.Name;
                    break;
                case nameof(UnbanCommand):
                    _unbanCommandName = cmd.Name;
                    break;
                case nameof(KickCommand):
                    _kickCommandName = cmd.Name;
                    break;
                case nameof(FlagClientCommand):
                    _flagCommandName = cmd.Name;
                    break;
                case nameof(UnflagClientCommand):
                    _unflagCommandName = cmd.Name;
                    break;
                case nameof(SetLevelCommand):
                    _setLevelCommandName = cmd.Name;
                    break;
                case "SayCommand":
                    _sayCommandName = cmd.Name;
                    break;
                case "OfflineMessageCommand":
                    _offlineMessageCommandName = cmd.Name;
                    break;
                case "SetClientTagCommand":
                    _setClientTagCommandName = cmd.Name;
                    break;
                case "AddClientNoteCommand":
                    _addClientNoteCommandName = cmd.Name;
                    break;
            }
        }
    }

    public async Task<ActionInfo> GetActionInfoAsync(string actionName, int? targetId, string meta,
        string? serverId = null)
    {
        var name = actionName.Replace("Form", "", StringComparison.OrdinalIgnoreCase);

        return name.ToLowerInvariant() switch
        {
            "ban" => GetBanInfo(),
            "edit" => await GetEditInfo(targetId),
            "kick" => GetKickInfo(),
            "addclientnote" => await GetAddClientNoteInfo(targetId),
            "flag" => GetFlagInfo(),
            "unflag" => GetUnflagInfo(),
            "unban" => GetUnbanInfo(),
            "login" => GetLoginInfo(),
            "setclienttag" => await GetSetClientTagInfo(targetId),
            "offlinemessage" => GetOfflineMessageInfo(),
            "generatelogintoken" => GetGenerateLoginTokenInfo(),
            "dynamicaction" => GetDynamicActionInfo(targetId, meta),
            "chat" => GetChatInfo(),
            "disabletwofactor" => GetDisableTwoFactorInfo(),
            _ => throw new ArgumentException($"Unknown action: {actionName}")
        };
    }

    public async Task<(bool Success, string Message)> ExecuteActionAsync(string actionName, int? targetId,
        Dictionary<string, object> formData, EFClient origin, string? serverId = null)
    {
        var name = actionName.Replace("Form", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Async", "", StringComparison.OrdinalIgnoreCase);

        return name.ToLowerInvariant() switch
        {
            "ban" => await ExecuteBan(targetId, formData, origin),
            "edit" => await ExecuteEdit(targetId, formData, origin),
            "kick" => await ExecuteKick(targetId, formData, origin),
            "addclientnote" => await ExecuteAddClientNote(targetId, formData, origin),
            "flag" => await ExecuteFlag(targetId, formData, origin),
            "unflag" => await ExecuteUnflag(targetId, formData, origin),
            "unban" => await ExecuteUnban(targetId, formData, origin),
            "setclienttag" => await ExecuteSetClientTag(targetId, formData, origin),
            "offlinemessage" => await ExecuteOfflineMessage(targetId, formData, origin),
            "generatelogintoken" => ExecuteGenerateLoginToken(origin),
            "dynamicaction" => await ExecuteDynamicAction(targetId, formData, origin),
            "chat" => await ExecuteChat(serverId, formData, origin),
            "disabletwofactor" => await ExecuteDisableTwoFactor(targetId, origin),
            _ => throw new ArgumentException($"Unknown action: {actionName}")
        };
    }

    #region Actions

    private ActionInfo GetDynamicActionInfo(int? targetId, string meta)
    {
        if (string.IsNullOrWhiteSpace(meta))
            throw new ArgumentException("Meta is required for dynamic actions");

        try
        {
            var metaDict = JsonSerializer.Deserialize<Dictionary<string, string>>(meta.TrimEnd('"').TrimStart('"'));
            if (metaDict is null)
                throw new ArgumentException("Failed to parse meta JSON");

            metaDict.TryGetValue(nameof(ActionInfo.ActionButtonLabel), out var label);
            metaDict.TryGetValue(nameof(ActionInfo.Name), out var name);
            metaDict.TryGetValue(nameof(ActionInfo.ShouldRefresh), out var refreshStr);
            metaDict.TryGetValue("Data", out var data);
            metaDict.TryGetValue("InteractionId", out var interactionId);
            metaDict.TryGetValue("Inputs", out var template);

            List<InputInfo>? additionalInputs = null;
            var inputKeys = string.Empty;

            if (!string.IsNullOrWhiteSpace(template))
            {
                additionalInputs = JsonSerializer.Deserialize<List<InputInfo>>(template);
            }

            if (additionalInputs != null)
            {
                inputKeys = string.Join(",", additionalInputs.Select(input => input.Name));
            }

            bool.TryParse(refreshStr, out var shouldRefresh);

            var inputs = new List<InputInfo>
            {
                new()
                {
                    Name = "InteractionId",
                    Value = interactionId,
                    Type = "hidden"
                },
                new()
                {
                    Name = "Data", // Case sensitive in formData?
                    Value = data,
                    Type = "hidden"
                },
                new()
                {
                    Name = "TargetId",
                    Value = targetId?.ToString(),
                    Type = "hidden"
                },
                new()
                {
                    Name = "CustomInputKeys",
                    Value = inputKeys,
                    Type = "hidden"
                }
            };

            if (additionalInputs?.Any() ?? false)
            {
                inputs.AddRange(additionalInputs);
            }

            return new ActionInfo
            {
                ActionButtonLabel = label ?? string.Empty,
                Name = name ?? string.Empty,
                Action = "DynamicAction",
                ShouldRefresh = shouldRefresh,
                Inputs = inputs
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing dynamic action meta");
            throw new InvalidOperationException($"Error parsing dynamic action meta: {ex.Message}", ex);
        }
    }

    private async Task<(bool, string)> ExecuteDynamicAction(int? targetId, Dictionary<string, object> formData,
        EFClient origin)
    {
        if (origin.ClientId < 1)
        {
            return (false, _localization["SERVER_COMMANDS_INTERCEPTED"]);
        }

        var interactionId = formData.TryGetValue("InteractionId", out var iId) ? iId?.ToString() : null;
        var data = formData.TryGetValue("Data", out var d) ? d?.ToString() : null;
        var inputKeysStr = formData.TryGetValue("CustomInputKeys", out var k) ? k?.ToString() : null;

        var inputs = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(inputKeysStr))
        {
            foreach (var key in inputKeysStr.Split(','))
            {
                if (formData.TryGetValue(key, out var val) && val != null &&
                    !string.IsNullOrWhiteSpace(val.ToString()))
                {
                    inputs[key] = val.ToString() ?? string.Empty;
                }
            }
        }

        var game = (Reference.Game?)null;
        if (targetId.HasValue)
        {
            var client = await _manager.GetClientService().Get(targetId.Value);
            game = client?.GameName;
        }

        if (interactionId != "command")
        {
            try
            {
                var result = await _interactionRegistration.ProcessInteraction(interactionId, origin.ClientId,
                    targetId, game, inputs, CancellationToken.None);
                return (true, result);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // Handle "command" interactions (generic command execution)
        var server = _manager.Servers.FirstOrDefault();
        if (server == null)
            return (false, "No servers available");

        var (success, response) =
            await _remoteCommandService.ExecuteWithResult(origin.ClientId, targetId, data, inputs.Values, server);

        var responseMsg = response != null ? string.Join("\n", response.Select(r => r.Response)) : "Executed";
        return (success, responseMsg);
    }

    private ActionInfo GetBanInfo()
    {
        return new ActionInfo
        {
            ActionButtonLabel = _localization["WEBFRONT_ACTION_BAN_NAME"],
            Name = _localization["WEBFRONT_ACTION_BAN_NAME"],
            Inputs =
            [
                new InputInfo { Name = "Reason", Label = _localization["WEBFRONT_ACTION_LABEL_REASON"] },
                new InputInfo
                {
                    Name = "PresetReason", Type = "select",
                    Label = _localization["WEBFRONT_ACTION_LABEL_PRESET_REASON"], Values = GetPresetPenaltyReasons()
                },

                new InputInfo
                {
                    Name = "Duration", Label = _localization["WEBFRONT_ACTION_LABEL_DURATION"], Type = "select",
                    Values = _appConfig.BanDurations
                        .Select((item, index) => new
                            { Id = (index + 1).ToString(), Value = item.HumanizeForCurrentCulture() })
                        .Append(new
                        {
                            Id = (_appConfig.BanDurations.Length + 1).ToString(),
                            Value = _localization["WEBFRONT_ACTION_SELECTION_PERMANENT"]
                        }).ToDictionary(duration => duration.Id, duration => duration.Value)
                }
            ],
            Action = "Ban",
            ShouldRefresh = true
        };
    }

    private async Task<(bool, string)> ExecuteBan(int? targetId, Dictionary<string, object> formData,
        EFClient origin)
    {
        if (targetId == null)
            throw new ArgumentNullException(nameof(targetId));

        var reason = formData.TryGetValue("Reason", out var r) ? r?.ToString() : null;
        var presetReason = formData.TryGetValue("PresetReason", out var pr) ? pr?.ToString() : null;
        var durationIndex = formData.TryGetValue("Duration", out var d) ? d?.ToString() : null;

        var finalReason = !string.IsNullOrEmpty(presetReason) ? presetReason : reason;

        string command;
        int.TryParse(durationIndex, out int idx);

        if (idx > _appConfig.BanDurations.Length)
        {
            command = $"{_appConfig.CommandPrefix}{_banCommandName} @{targetId} {finalReason}";
        }
        else
        {
            if (idx < 1)
                idx = 1;
            var durationSpan = _appConfig.BanDurations[idx - 1];
            var durationValue =
                durationSpan.TotalMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                _localization["GLOBAL_TIME_MINUTES"][0];

            command = $"{_appConfig.CommandPrefix}{_tempbanCommandName} @{targetId} {durationValue} {finalReason}";
        }

        return await ExecuteCommand(command, origin);
    }

    private async Task<ActionInfo> GetEditInfo(int? targetId)
    {
        return new ActionInfo
        {
            ActionButtonLabel = _localization["WEBFRONT_ACTION_LABEL_EDIT"],
            Name = _localization["WEBFRONT_ACTION_LABEL_EDIT"],
            Inputs =
            [
                new InputInfo
                {
                    Name = "level",
                    Label = _localization["WEBFRONT_PROFILE_LEVEL"],
                    Type = "select",
                    Values = Enum.GetValues(typeof(EFClient.Permission)).OfType<EFClient.Permission>()
                        .Where(p => p != EFClient.Permission.Banned)
                        .Where(p => p != EFClient.Permission.Flagged)
                        .ToDictionary(p => p.ToString(), p => p.ToLocalizedLevelName())
                }
            ],
            Action = "Edit",
            ShouldRefresh = true
        };
    }

    private async Task<(bool, string)> ExecuteEdit(int? targetId, Dictionary<string, object> formData,
        EFClient origin)
    {
        if (targetId == null)
            throw new ArgumentNullException(nameof(targetId));
        var level = formData["level"]?.ToString();

        var command = $"{_appConfig.CommandPrefix}{_setLevelCommandName} @{targetId} {level}";
        return await ExecuteCommand(command, origin);
    }

    private ActionInfo GetKickInfo()
    {
        return new ActionInfo
        {
            ActionButtonLabel = _localization["WEBFRONT_ACTION_KICK_NAME"],
            Name = _localization["WEBFRONT_ACTION_KICK_NAME"],
            Inputs =
            [
                new InputInfo { Name = "Reason", Label = _localization["WEBFRONT_ACTION_LABEL_REASON"] },
                new InputInfo
                {
                    Name = "PresetReason", Type = "select",
                    Label = _localization["WEBFRONT_ACTION_LABEL_PRESET_REASON"],
                    Values = GetPresetPenaltyReasons()
                }
            ],
            Action = "Kick",
            ShouldRefresh = true
        };
    }

    private async Task<(bool, string)> ExecuteKick(int? targetId, Dictionary<string, object> formData,
        EFClient origin)
    {
        if (targetId == null)
            throw new ArgumentNullException(nameof(targetId));
        var reason = formData.TryGetValue("Reason", out var r) ? r?.ToString() : null;
        var preset = formData.TryGetValue("PresetReason", out var pr) ? pr?.ToString() : null;
        var final = !string.IsNullOrEmpty(reason) ? reason : preset;

        var command = $"{_appConfig.CommandPrefix}{_kickCommandName} @{targetId} {final}";
        return await ExecuteCommand(command, origin);
    }

    private async Task<ActionInfo> GetAddClientNoteInfo(int? targetId)
    {
        string existingNote = "";
        if (targetId.HasValue)
        {
            var response =
                await _metaService.GetPersistentMetaValue<ClientNoteMetaResponse>("ClientNotes", targetId.Value);
            existingNote = response?.Note ?? string.Empty;
        }

        return new ActionInfo
        {
            ActionButtonLabel = _localization["WEBFRONT_CONFIGURATION_BUTTON_SAVE"],
            Name = _localization["WEBFRONT_PROFILE_CONTEXT_MENU_NOTE"],
            Inputs =
            [
                new InputInfo
                {
                    Name = "note",
                    Label = _localization["WEBFRONT_ACTION_NOTE_FORM_NOTE"],
                    Value = existingNote,
                    Type = "textarea"
                }
            ],
            Action = "AddClientNote",
            ShouldRefresh = true
        };
    }

    private async Task<(bool, string)> ExecuteAddClientNote(int? targetId, Dictionary<string, object> formData,
        EFClient origin)
    {
        if (targetId == null)
            throw new ArgumentNullException(nameof(targetId));
        var note = formData["note"]?.ToString();

        var command = $"{_appConfig.CommandPrefix}{_addClientNoteCommandName} @{targetId} {note}";
        return await ExecuteCommand(command, origin);
    }

    private ActionInfo GetFlagInfo()
    {
        return new ActionInfo
        {
            ActionButtonLabel = _localization["WEBFRONT_ACTION_FLAG_NAME"],
            Name = _localization["WEBFRONT_ACTION_FLAG_NAME"],
            Inputs =
            [
                new InputInfo { Name = "Reason", Label = _localization["WEBFRONT_ACTION_LABEL_REASON"] },
                new InputInfo
                {
                    Name = "PresetReason", Type = "select",
                    Label = _localization["WEBFRONT_ACTION_LABEL_PRESET_REASON"], Values = GetPresetPenaltyReasons()
                }
            ],
            Action = "Flag",
            ShouldRefresh = true
        };
    }

    private async Task<(bool, string)> ExecuteFlag(int? targetId, Dictionary<string, object> formData,
        EFClient origin)
    {
        if (targetId == null)
            throw new ArgumentNullException(nameof(targetId));
        var reason = formData.TryGetValue("Reason", out var r) ? r?.ToString() : null;
        var preset = formData.TryGetValue("PresetReason", out var pr) ? pr?.ToString() : null;
        var final = !string.IsNullOrEmpty(reason) ? reason : preset;

        var command = $"{_appConfig.CommandPrefix}{_flagCommandName} @{targetId} {final}";
        return await ExecuteCommand(command, origin);
    }

    private ActionInfo GetUnflagInfo()
    {
        return new ActionInfo
        {
            ActionButtonLabel = _localization["WEBFRONT_ACTION_UNFLAG_NAME"],
            Name = _localization["WEBFRONT_ACTION_UNFLAG_NAME"],
            Inputs = [new InputInfo { Name = "Reason", Label = _localization["WEBFRONT_ACTION_LABEL_REASON"] }],
            Action = "Unflag",
            ShouldRefresh = true
        };
    }

    private async Task<(bool, string)> ExecuteUnflag(int? targetId, Dictionary<string, object> formData,
        EFClient origin)
    {
        if (targetId == null)
            throw new ArgumentNullException(nameof(targetId));
        var reason = formData.TryGetValue("Reason", out var r) ? r?.ToString() : null;

        var command = $"{_appConfig.CommandPrefix}{_unflagCommandName} @{targetId} {reason}";
        return await ExecuteCommand(command, origin);
    }

    private ActionInfo GetUnbanInfo()
    {
        return new ActionInfo
        {
            ActionButtonLabel = _localization["WEBFRONT_ACTION_UNBAN_NAME"],
            Name = _localization["WEBFRONT_ACTION_UNBAN_NAME"],
            Inputs = [new InputInfo { Name = "Reason", Label = _localization["WEBFRONT_ACTION_LABEL_REASON"] }],
            Action = "Unban",
            ShouldRefresh = true
        };
    }

    private async Task<(bool, string)> ExecuteUnban(int? targetId, Dictionary<string, object> formData,
        EFClient origin)
    {
        if (targetId == null)
            throw new ArgumentNullException(nameof(targetId));
        var reason = formData.TryGetValue("Reason", out var r) ? r?.ToString() : null;

        var command = $"{_appConfig.CommandPrefix}{_unbanCommandName} @{targetId} {reason}";
        return await ExecuteCommand(command, origin);
    }

    private ActionInfo GetLoginInfo()
    {
        return new ActionInfo
        {
            Name = _localization["WEBFRONT_NAV_TITLE_LOGIN"],
            ActionButtonLabel = _localization["WEBFRONT_NAV_TITLE_LOGIN"],
            Inputs =
            [
                new InputInfo
                {
                    Name = "Id", Label = _localization["WEBFRONT_ACTION_LABEL_ID"],
                    Placeholder = _localization["WEBFRONT_ACTION_LABEL_ID"]
                },

                new InputInfo
                {
                    Name = "Password", Label = _localization["WEBFRONT_ACTION_LABEL_PASSWORD"], Type = "password",
                    Placeholder = _localization["WEBFRONT_ACTION_LABEL_PASSWORD"]
                }
            ],
            Action = "Login"
        };
    }

    private class TagMetaDto
    {
        public int Id { get; set; }
        public required string Value { get; set; }
    }

    private async Task<ActionInfo> GetSetClientTagInfo(int? targetId)
    {
        var tags = await _metaService.GetPersistentMetaValue<List<TagMetaDto>>(EFMeta.ClientTagNameV2) ??
                   [];
        string existingTag = "";
        if (targetId.HasValue)
        {
            var meta = await _metaService.GetPersistentMetaByLookup(EFMeta.ClientTagV2, EFMeta.ClientTagNameV2,
                targetId.Value, CancellationToken.None);
            existingTag = meta?.Value ?? string.Empty;
        }

        return new ActionInfo
        {
            ActionButtonLabel = _localization["WEBFRONT_ACTION_SET_CLIENT_TAG_SUBMIT"],
            Name = _localization["WEBFRONT_PROFILE_CONTEXT_MENU_TAG"],
            Inputs =
            [
                new InputInfo
                {
                    Name = "clientTag",
                    Type = "select",
                    Label = _localization["WEBFRONT_ACTION_SET_CLIENT_TAG_FORM_TAG"],
                    Values = tags.ToDictionary(
                        item => item.Value == existingTag ? $"!selected!{item.Value}" : item.Value,
                        item => item.Value)
                }
            ],
            Action = "SetClientTag",
            ShouldRefresh = true
        };
    }

    private async Task<(bool, string)> ExecuteSetClientTag(int? targetId, Dictionary<string, object> formData,
        EFClient origin)
    {
        if (targetId == null)
            throw new ArgumentNullException(nameof(targetId));
        var tag = formData["clientTag"]?.ToString();

        var command = $"{_appConfig.CommandPrefix}{_setClientTagCommandName} @{targetId} {tag}";
        return await ExecuteCommand(command, origin);
    }

    private ActionInfo GetOfflineMessageInfo()
    {
        return new ActionInfo
        {
            ActionButtonLabel = _localization["WEBFRONT_ACTION_OFFLINE_MESSAGE_FORM_SUBMIT"],
            Name = _localization["WEBFRONT_ACTION_OFFLINE_MESSAGE_BUTTON_COMPOSE"],
            Inputs =
            [
                new InputInfo
                {
                    Name = "message",
                    Label = _localization["WEBFRONT_ACTION_OFFLINE_MESSAGE_FORM_CONTENT"],
                }
            ],
            Action = "OfflineMessage",
            ShouldRefresh = false
        };
    }

    private async Task<(bool, string)> ExecuteOfflineMessage(int? targetId, Dictionary<string, object> formData,
        EFClient origin)
    {
        if (targetId == null)
            throw new ArgumentNullException(nameof(targetId));
        var message = formData["message"]?.ToString();

        var command = $"{_appConfig.CommandPrefix}{_offlineMessageCommandName} @{targetId} {message}";
        return await ExecuteCommand(command, origin);
    }

    private ActionInfo GetGenerateLoginTokenInfo()
    {
        return new ActionInfo
        {
            ActionButtonLabel = _localization["WEBFRONT_ACTION_LABEL_GENERATE_TOKEN"],
            Name = "GenerateLoginToken",
            Action = "GenerateLoginTokenAsync",
            Inputs = []
        };
    }

    private (bool, string) ExecuteGenerateLoginToken(EFClient origin)
    {
        var state = _manager.TokenAuthenticator.GenerateNextToken(new SharedLibraryCore.Helpers.TokenIdentifier
        {
            ClientId = origin.ClientId
        });

        return (true, string.Format(_localization["COMMANDS_GENERATETOKEN_SUCCESS"],
            state.Token,
            $"{state.RemainingTime} {_localization["GLOBAL_MINUTES"]}",
            origin.ClientId));
    }

    private ActionInfo GetChatInfo()
    {
        var info = new ActionInfo
        {
            Name = _localization["WEBFRONT_ACTION_CHAT_NAME"],
            ActionButtonLabel = _localization["WEBFRONT_ACTION_CHAT_SUBMIT"],
            Action = "Chat",
            Inputs =
            [
                new InputInfo
                {
                    Name = "Message",
                    Label = _localization["WEBFRONT_ACTION_CHAT_MESSAGE"],
                    Type = "text",
                    Placeholder = _localization["WEBFRONT_ACTION_CHAT_PLACEHOLDER"]
                }
            ]
        };
        return info;
    }

    private async Task<(bool, string)> ExecuteChat(string? serverId, Dictionary<string, object> formData,
        EFClient origin)
    {
        if (string.IsNullOrEmpty(serverId))
        {
            return (false, "Server ID is required");
        }

        var server = _manager.Servers.FirstOrDefault(s => $"{s.ListenAddress}:{s.ListenPort}" == serverId);
        if (server == null)
        {
            return (false, "Server not found");
        }

        var message = formData.TryGetValue("Message", out var m) ? m?.ToString() : null;
        if (string.IsNullOrWhiteSpace(message))
        {
            return (false, "Message is required");
        }

        var command = $"{_appConfig.CommandPrefix}{_sayCommandName} {message}";

        var (success, response) =
            await _remoteCommandService.ExecuteWithResult(origin.ClientId, null, command, [], server);

        return response == null
            ? (true, "Message Sent")
            : (success, string.Join("\n", response.Select(r => r.Response)));
    }

    private ActionInfo GetDisableTwoFactorInfo()
    {
        return new ActionInfo
        {
            ActionButtonLabel = _localization["WEBFRONT_ACTION_LABEL_DISABLE_2FA"],
            Name = _localization["WEBFRONT_ACTION_LABEL_DISABLE_2FA"],
            Inputs = [],
            Action = "DisableTwoFactor",
            ShouldRefresh = true
        };
    }

    private async Task<(bool, string)> ExecuteDisableTwoFactor(int? targetId, EFClient origin)
    {
        if (targetId == null)
            throw new ArgumentNullException(nameof(targetId));

        if (origin.Level < EFClient.Permission.Owner)
        {
            return (false, _localization["WEBFRONT_RESPONSE_UNAUTHORIZED"]);
        }

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(_appConfig.WebfrontBindUrl ?? "http://localhost:1624");
        
        var client = await _manager.GetClientService().Get(targetId.Value);
        client.TwoFactorSecret = null;
        client.TwoFactorBackupCodes = null;
        await _manager.GetClientService().Update(client);

        return (true, _localization["WEBFRONT_ACTION_DISABLE_2FA_SUCCESS"]);
    }

    #endregion

    private Dictionary<string, string> GetPresetPenaltyReasons()
    {
        var reasons = _appConfig.PresetPenaltyReasons.Values
            .Concat(_appConfig.GlobalRules ?? [])
            .Concat(_appConfig.Servers?.SelectMany(server => server.Rules ?? []) ??
                    [])
            .Distinct()
            .Where(r => !string.IsNullOrEmpty(r))
            .ToDictionary(k => k, k => k);

        return new Dictionary<string, string> { { "", "" } }.Concat(reasons).ToDictionary(k => k.Key, k => k.Value);
    }

    private async Task<(bool, string)> ExecuteCommand(string command, EFClient origin)
    {
        var server = _manager.Servers.FirstOrDefault();
        if (server == null)
            throw new Exception("No servers available");

        var (success, response) = await _remoteCommandService.ExecuteWithResult(origin.ClientId, null, command,
            [], server);

        if (response == null)
            return (true, "Command Executed");

        return (success, string.Join("\n", response.Select(r => r.Response)));
    }
}
