using System;
using System.Collections.Generic;
using System.Linq;
using Data.Models.Client;
using SharedLibraryCore.Interfaces;
using InteractionCallback = System.Func<int?, Data.Models.Reference.Game?, System.Threading.CancellationToken, System.Threading.Tasks.Task<string>>;
using ScriptInteractionCallback = System.Func<int?, Data.Models.Reference.Game?, System.Threading.CancellationToken, System.Threading.Tasks.Task<string>>;

namespace SharedLibraryCore.Helpers;

public class InteractionData : IInteractionData
{
    public int? EntityId { get; set; }
    public bool Enabled { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string DisplayMeta { get; set; }
    public string ActionPath { get; set; }
    public Dictionary<string, string> ActionMeta { get; set; } = new();
    public string ActionUri => ActionPath + "?" + string.Join('&', ActionMeta.Select(kvp => $"{kvp.Key}={kvp.Value}"));
    public EFClient.Permission? MinimumPermission { get; set; }
    public string PermissionEntity { get; set; } = "Interaction";
    public string PermissionAccess { get; set; } = "Read";
    public string Source { get; set; }
    public InteractionCallback Action { get; set; }
    public Delegate ScriptAction { get; set; }
}
