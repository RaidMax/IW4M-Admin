using System;

namespace Data.Models;

public class DatedRecord : IdentifierRecord
{
    public DateTimeOffset CreatedDateTime { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedDateTime { get; set; }
    public override long Id { get; }
}
