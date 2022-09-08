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
    Task<IEnumerable<IInteractionData>> GetInteractions(int? clientId = null,
        Reference.Game? game = null, CancellationToken token = default);
    Task<string> ProcessInteraction(string interactionId, int? clientId = null, Reference.Game? game = null, CancellationToken token = default);
}
