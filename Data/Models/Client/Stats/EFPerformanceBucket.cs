using System.ComponentModel.DataAnnotations;

namespace Data.Models.Client.Stats;

public class EFPerformanceBucket
{
    [Key]
    public int PerformanceBucketId { get; set; }
    
    [MaxLength(256)]
    public string BucketCode { get; set; }
    
    [MaxLength(256)]
    public string BucketName { get; set; }
}
