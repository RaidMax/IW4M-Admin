using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Data.Models;

namespace SharedLibraryCore.Interfaces;

public interface IInteractionRegistration
{
    void RegisterScriptInteraction(string interactionName, string source, Delegate interactionRegistration);
    void RegisterInteraction(string interactionName, Func<int?, Reference.Game?, CancellationToken, Task<IInteractionData>> interactionRegistration);
    void UnregisterInteraction(string interactionName);
    Task<IEnumerable<IInteractionData>> GetInteractions(string interactionPrefix = null, int? clientId = null,
        Reference.Game? game = null, CancellationToken token = default);
    Task<string> ProcessInteraction(string interactionId, int originId, int? targetId = null, Reference.Game? game = null, IDictionary<string, string> meta = null, CancellationToken token = default);
}
