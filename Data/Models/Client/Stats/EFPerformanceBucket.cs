using System.ComponentModel.DataAnnotations;

namespace Data.Models.Client.Stats;

public class EFPerformanceBucket
{
    [Key] public int PerformanceBucketId { get; set; }

    /// <summary>
    /// Identifier for Bucket
    /// </summary>
    [MaxLength(256)]
    public string Code { get; set; }

    /// <summary>
    /// Friendly name for Bucket
    /// </summary>
    [MaxLength(256)]
    public string Name { get; set; }
}
