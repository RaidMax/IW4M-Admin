namespace WebfrontCore.Core.Auth;

/// <summary>
/// Marks a login action. Requests to it are limited per remote address by the global rate limiter
/// registered in Program.ConfigureServices, so passwords and login tokens cannot be guessed at speed.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LoginAttemptLimitAttribute : Attribute
{
    /// <summary>attempts allowed per remote address in each window</summary>
    public const int PermitLimit = 10;

    /// <summary>length of the window</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
}
