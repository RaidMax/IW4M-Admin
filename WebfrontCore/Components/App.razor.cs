using Microsoft.AspNetCore.Components;

namespace WebfrontCore.Components;

public partial class App
{
    [Inject] public SharedLibraryCore.Configuration.ApplicationConfiguration AppConfig { get; set; }

    private string GetThemeColor()
    {
        var primaryColor = !string.IsNullOrEmpty(AppConfig.Webfront.PrimaryColor)
            ? AppConfig.Webfront.PrimaryColor
            : AppConfig.Webfront.SecondaryColor;
        return primaryColor?.ToLowerInvariant() switch
        {
            "slate" => "#64748b",
            "gray" => "#6b7280",
            "zinc" => "#71717a",
            "neutral" => "#737373",
            "stone" => "#78716c",
            "red" => "#ef4444",
            "orange" => "#f97316",
            "amber" => "#f59e0b",
            "yellow" => "#eab308",
            "lime" => "#84cc16",
            "green" => "#22c55e",
            "emerald" => "#10b981",
            "teal" => "#14b8a6",
            "cyan" => "#06b6d4",
            "sky" => "#0ea5e9",
            "blue" => "#3b82f6",
            "indigo" => "#6366f1",
            "violet" => "#8b5cf6",
            "purple" => "#a855f7",
            "fuchsia" => "#d946ef",
            "pink" => "#ec4899",
            "rose" => "#f43f5e",
            _ => !string.IsNullOrEmpty(primaryColor) && (primaryColor.StartsWith("#") ||
                                                         primaryColor.StartsWith("hsl") ||
                                                         primaryColor.StartsWith("rgb"))
                ? primaryColor
                : "#3b82f6" // Default to Blue
        };
    }
}
