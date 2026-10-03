using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static class OpenApiChecks
{
    internal static async Task Run()
    {
        var builder = WebApplication.CreateBuilder(["--urls", "http://127.0.0.1:0"]);
        builder.Logging.ClearProviders();
        builder.Services.AddControllers().AddApplicationPart(typeof(WebfrontCore.Controllers.API.StatsController).Assembly);
        builder.Services.AddOpenApi();
        await using var app = builder.Build();
        app.MapControllers();
        app.MapOpenApi();
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
                .Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(20) };
            using var response = await client.GetAsync("/openapi/v1.json");
            Check.Equal(true, response.IsSuccessStatusCode, "Patched OpenAPI library generates the host API document");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Check.Equal(true, document.RootElement.GetProperty("paths").TryGetProperty("/api/stats/top", out _),
                "Generated API document retains the statistics leaderboard route");
        }
        finally { await app.StopAsync(); }
    }
}
