using System;

public class BanInfo
{
    public string OffenderName { get; set; }
    public int OffenderId { get; set; }
    public string PunisherName { get; set; }
    public int? PunisherId { get; set; }
    public string Offense { get; set; }
    public DateTime? DateTime { get; set; }
    public long? TimeStamp => DateTime.HasValue ? new DateTimeOffset(DateTime.Value, TimeSpan.Zero).ToUnixTimeSeconds() : null;
}
