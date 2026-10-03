using System.Reflection;
using System.Runtime.CompilerServices;
using IW4MAdmin.Application;
using IW4MAdmin;
using IW4MAdmin.Application.Services;
using IW4MAdmin.Plugins.Stats.Client;
using Microsoft.Extensions.Logging.Abstractions;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Events.Game;

static class FlowChecks
{
    static IW4MServer EmptyServer() => (IW4MServer)RuntimeHelpers.GetUninitializedObject(typeof(IW4MServer));

    public static async Task MatchScoreTest()
    {
        var server = EmptyServer();
        var client = new EFClient { ClientId = 42, NetworkId = 123, Score = 100 };
        client.SetAdditionalProperty("SessionScores", new List<(int, DateTime)>());
        typeof(Server).GetProperty("Clients")!.SetValue(server, new List<EFClient> { client });
        var ctor = typeof(HitCalculator).GetConstructors().Single();
        var calc = (HitCalculator)ctor.Invoke(ctor.GetParameters()
            .Select(p => p.Name == "logger" ? (object)NullLogger<HitCalculator>.Instance : null).ToArray());
        await calc.CalculateForEvent(new MatchEndEvent { Owner = server });
        Check.Equal(1, client.GetAdditionalProperty<List<(int, DateTime)>>("SessionScores").Count, "Match-end session score snapshot");
    }
}
