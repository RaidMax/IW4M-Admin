using System;

namespace SharedLibraryCore.Events;

public abstract class CoreEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public Guid? CorrelationId { get; init; }
    public object Source { get; init; }
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}
