using Newtonsoft.Json;

namespace Mute;

public class MuteStateMeta
{
    public int ClientId { get; set; }
    public string CleanedName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime? Expiration { get; set; }
    public int AdminId { get; set; }
    public MuteState MuteState { get; set; }
    [JsonIgnore]
    public bool CommandExecuted { get; set; }
}

public enum MuteState
{
    Muted,
    Unmuting,
    Unmuted
}
