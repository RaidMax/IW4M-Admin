namespace WebfrontCore.Controllers.API.Models;

/// <summary>
/// Request model for adding a server dynamically
/// </summary>
public class AddServerRequest
{
    /// <summary>
    /// Server IP address
    /// </summary>
    public required string IPAddress { get; set; }

    /// <summary>
    /// Server RCON port
    /// </summary>
    public required int Port { get; set; }

    /// <summary>
    /// RCON password
    /// </summary>
    public required string Password { get; set; }

    /// <summary>
    /// RCon parser version (e.g., "IW4", "T6", "IW4x")
    /// </summary>
    public required string RConParserVersion { get; set; }

    /// <summary>
    /// Event parser version (e.g., "IW4", "T6", "IW4x")
    /// </summary>
    public required string EventParserVersion { get; set; }

    /// <summary>
    /// Whether to persist the server to configuration (default: false for temporal changes)
    /// </summary>
    public bool PersistToConfiguration { get; set; } = true;

    /// <summary>
    /// Optional custom hostname
    /// </summary>
    public string? CustomHostname { get; set; }

    /// <summary>
    /// Optional game log server URL
    /// </summary>
    public string? GameLogServerUrl { get; set; }

    /// <summary>
    /// Optional manual log path
    /// </summary>
    public string? ManualLogPath { get; set; }

    /// <summary>
    /// Reserved slot number
    /// </summary>
    public int ReservedSlotNumber { get; set; }
}

/// <summary>
/// Response model for add server operation
/// </summary>
public class AddServerResponse
{
    public required string ServerId { get; set; }
    public required string Hostname { get; set; }
    public required string Game { get; set; }
    public bool Persisted { get; set; }
}
