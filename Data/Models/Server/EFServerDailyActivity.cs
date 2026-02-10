using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Models.Server;

public class EFServerDailyActivity : SharedEntity
{
    [Key]
    public long Id { get; set; }
    public long ServerId { get; set; }
    [ForeignKey(nameof(ServerId))]
    public virtual EFServer Server { get; set; }
    public DateTime Date { get; set; }
    public long PlayTimeMinutes { get; set; }
    public int UniqueClientCount { get; set; }
    public int ConnectionCount { get; set; }
}
