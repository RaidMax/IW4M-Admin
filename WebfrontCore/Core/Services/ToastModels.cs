namespace WebfrontCore.Core.Services;

public enum ToastType
{
    Success,
    Error,
    Warning,
    Info
}

public class ToastMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; }
    public string Message { get; set; }
    public ToastType Type { get; set; }
    public int Duration { get; set; } = 5000;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
