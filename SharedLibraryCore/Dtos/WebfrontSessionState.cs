using System;
using Data.Models.Client;

namespace SharedLibraryCore.Dtos
{
    /// <summary>
    ///     the database facts that decide whether a webfront session cookie of a client is still valid
    /// </summary>
    /// <param name="Level">current permission level</param>
    /// <param name="Active">whether the client is active</param>
    /// <param name="SessionsRevokedAt">sessions issued at or before this time (UTC) are invalid</param>
    public record WebfrontSessionState(EFClient.Permission Level, bool Active, DateTime? SessionsRevokedAt);
}
