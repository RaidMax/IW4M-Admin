using System;
using System.Collections.Generic;
using Data.Models.Client;
using InteractionCallback = System.Func<int, int?, Data.Models.Reference.Game?, System.Collections.Generic.IDictionary<string,string>, System.Threading.CancellationToken, System.Threading.Tasks.Task<string>>;

namespace SharedLibraryCore.Interfaces;

public interface IInteractionData
{
    int? EntityId { get; }
    bool Enabled { get; }
    string Name { get; }
    string Description { get; }
    string DisplayMeta { get; }
    string ActionPath { get; }
    Dictionary<string, string> ActionMeta { get; }
    string ActionUri { get; }
    EFClient.Permission? MinimumPermission { get; }
    string PermissionEntity { get; }
    string PermissionAccess { get; }
    string Source { get; }
    InteractionCallback Action { get; }
    Delegate ScriptAction { get; }
}
