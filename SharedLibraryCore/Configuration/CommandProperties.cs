using System;
using System.Text.Json.Serialization;
using SharedLibraryCore.Helpers;
using static Data.Models.Client.EFClient;
using static SharedLibraryCore.Server;

namespace SharedLibraryCore.Configuration;

/// <summary>
///     Config driven command properties
/// </summary>
public class CommandProperties
{
    /// <summary>
    ///     Specifies the command name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    ///     Alias of this command
    /// </summary>
    public string Alias { get; set; }

    /// <summary>
    ///     Specifies the minimum permission level needed to execute the
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Permission MinimumPermission { get; set; }

    /// <summary>
    ///     Indicates if the command can be run by another user (impersonation)
    /// </summary>
    public bool AllowImpersonation { get; set; }

    /// <summary>
    ///     Specifies the games supporting the functionality of the command
    /// </summary>
    [JsonConverter(typeof(GameArrayJsonConverter))]
    public Game[] SupportedGames { get; set; } = Array.Empty<Game>();
}
