using System;

namespace Data.Abstractions
{
    public class IAuditFields
    {
        DateTime CreatedDateTime { get; set; }
        DateTime? UpdatedDateTime { get; set; } 
    }
}