using System;
using Data.Models.Client;

namespace SharedLibraryCore.Alerts;

public class Alert
{
    public enum AlertCategory
    {
        Information,
        Warning,
        Error,
        Message,
    }

    public class AlertState
    {
        public Guid AlertId { get; } = Guid.NewGuid();
        public AlertCategory Category { get; set; }
        public DateTime OccuredAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public string Message { get; set; }
        public string Source { get; set; } 
        public int? RecipientId { get; set; }
        public int? SourceId { get; set; }
        public int? ReferenceId { get; set; }
        public bool? Delivered { get; set; }
        public bool? Consumed { get; set; }
        public EFClient.Permission? MinimumPermission { get; set; }
        public string Type { get; set; }
        public static AlertState Build() => new();
    }
}
