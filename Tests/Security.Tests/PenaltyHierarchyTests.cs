using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Security.Tests.Infrastructure;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using WebfrontCore.Core.Services;
using EFClient = SharedLibraryCore.Database.Models.EFClient;
using Permission = Data.Models.Client.EFClient.Permission;

namespace Security.Tests;

/// <summary>
/// S5/S7: level checks made by the penalty helpers on EFClient (SharedLibraryCore/PartialEntities/EFClient.cs),
/// and the web unban route that relies on them (POST api/penalty/unban/{targetId}).
/// </summary>
public class PenaltyHierarchyTests
{
    private static (EFClient Sender, EFClient Target, ManagerHarness Harness) Pair(Permission sender, Permission target)
    {
        var harness = new ManagerHarness();
        var server = harness.AddServer();
        return (new EFClient { ClientId = 11, Level = sender, CurrentServer = server },
            new EFClient { ClientId = 10, Level = target, CurrentServer = server }, harness);
    }

    public static TheoryData<string, Permission, Permission> HierarchyCases()
    {
        var data = new TheoryData<string, Permission, Permission>();
        foreach (var action in new[] { "ban", "kick", "tempban", "flag" })
        {
            data.Add(action, Permission.Trusted, Permission.Administrator);
            data.Add(action, Permission.Moderator, Permission.Moderator);
            data.Add(action, Permission.Administrator, Permission.Owner);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(HierarchyCases))]
    public void Penalty_action_refuses_an_equal_or_higher_target(string action, Permission sender, Permission target)
    {
        var (s, t, _) = Pair(sender, target);

        var result = action switch
        {
            "ban" => t.Ban("r", s, false),
            "kick" => t.Kick("r", s),
            "tempban" => t.TempBan("r", TimeSpan.FromHours(1), s),
            _ => t.Flag("r", s)
        };

        Assert.Equal(GameEvent.EventFailReason.Permission, result.FailReason);
    }

    [Fact]
    public void SetLevel_refuses_a_higher_target()
    {
        var (s, t, _) = Pair(Permission.Moderator, Permission.Administrator);

        Assert.Equal(GameEvent.EventFailReason.Permission, t.SetLevel(Permission.User, s).FailReason);
        Assert.Equal(Permission.Administrator, t.Level);
    }

    /// <summary>
    /// `!unban` is a SeniorAdmin command. EFClient.Unban is the only level check on the web unban route,
    /// so it has to refuse a sender who could not run the command.
    /// </summary>
    [Theory]
    [InlineData(Permission.User)]
    [InlineData(Permission.Trusted)]
    [InlineData(Permission.Moderator)]
    public void Unban_refuses_a_sender_below_the_unban_command_level(Permission sender)
    {
        var (s, t, _) = Pair(sender, Permission.Banned);

        var unban = t.Unban("let me back in", s);

        Assert.True(unban.FailReason == GameEvent.EventFailReason.Permission,
            $"a {sender} sender produced an Unban event for a Banned target with FailReason={unban.FailReason}; `!unban` itself requires SeniorAdmin");
    }

    /// <summary>
    /// End to end below the controller: WebfrontDataService.UnbanClientAsync is what
    /// PenaltyController.UnbanAsync ([Authorize] only, no policy) calls with the caller's cookie identity.
    /// </summary>
    [Fact]
    public async Task Web_unban_by_a_Trusted_session_does_not_dispatch_an_unban()
    {
        await using var database = await TestDatabase.CreateAsync();
        var trustedId = await database.AddClientAsync(Permission.Trusted, "trusted");
        var bannedId = await database.AddClientAsync(Permission.Banned, "banned");

        var harness = new ManagerHarness(database.Clients);
        harness.AddServer();

        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.Sid, trustedId.ToString()),
            new Claim(ClaimTypes.Role, nameof(Permission.Trusted))
        ], "login");
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var service = NewDataService(harness, database, accessor);
        await service.UnbanClientAsync(bannedId, "let me back in");

        var dispatched = harness.GameEvents.Where(e =>
            e.Type == GameEvent.EventType.Unban && e.FailReason == GameEvent.EventFailReason.None).ToList();
        Assert.True(dispatched.Count == 0,
            $"a Trusted web session dispatched {dispatched.Count} unfailed Unban event(s) for client {bannedId} (origin level {dispatched.FirstOrDefault()?.Origin.Level})");
    }

    internal static WebfrontDataService NewDataService(ManagerHarness harness, TestDatabase database,
        IHttpContextAccessor accessor, ApplicationConfiguration? configuration = null,
        ITwoFactorAuthService? twoFactor = null)
    {
        var translations = new SharedLibraryCore.Localization.TranslationLookup { Set = new Dictionary<string, string>() };
        return new WebfrontDataService(harness.Manager, null!, null!, null!, database.Clients, null!, null!, null!,
            null!, null!, null!, accessor, null!, null!, database.Factory, null!, null!, null!, translations,
            configuration ?? harness.Configuration, [], [], null!, twoFactor!);
    }
}
