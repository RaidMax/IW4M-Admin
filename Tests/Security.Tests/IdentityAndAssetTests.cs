using Data.Models;
using Microsoft.EntityFrameworkCore;
using Security.Tests.Infrastructure;
using SharedLibraryCore.Plugins;
using Permission = Data.Models.Client.EFClient.Permission;

namespace Security.Tests;

/// <summary>
/// S2/S8: a NetworkId reported by one game must not resolve to a privileged client of another game.
/// S1/S8: one plugin bundle's web assets must not be replaceable by another bundle.
/// </summary>
public class IdentityAndAssetTests
{
    [Fact]
    public async Task NetworkId_of_an_Owner_in_one_game_is_not_that_Owner_in_another_game()
    {
        await using var database = await TestDatabase.CreateAsync();
        var ownerId = await database.AddClientAsync(Permission.Owner, "owner"); // fixture clients are created as IW4
        long networkId;
        await using (var context = database.Factory.CreateContext())
        {
            networkId = (await context.Clients.SingleAsync(c => c.ClientId == ownerId)).NetworkId;
        }

        var sameGame = await database.Clients.GetUnique(networkId, Reference.Game.IW4);
        var otherGame = await database.Clients.GetUnique(networkId, Reference.Game.T6);

        Assert.Equal(ownerId, sameGame.ClientId);
        Assert.Equal(Permission.Owner, sameGame.Level);
        Assert.Null(otherGame);
    }

    /// <summary>
    /// Assets are served at /_content/{bundle id}/{path}. The manifest id is only checked for being non-blank
    /// (SharedLibraryCore/Plugins/PluginBundleLoader.cs ReadManifest), so an id can contain a path separator.
    /// </summary>
    [Fact]
    public void Bundle_cannot_replace_another_bundles_served_asset_through_its_id()
    {
        var storage = new InMemoryPluginAssetStorage();
        storage.RegisterPlugin("Victim", new Dictionary<string, byte[]> { ["js/app.js"] = "victim"u8.ToArray() });

        // a second bundle whose manifest id is "Victim/js" shipping wwwroot/app.js
        storage.RegisterPlugin("Victim/js", new Dictionary<string, byte[]> { ["app.js"] = "attacker"u8.ToArray() });

        Assert.True(storage.TryGet("Victim/js/app.js", out var served));
        var text = System.Text.Encoding.UTF8.GetString(served);
        Assert.True(text == "victim",
            $"/_content/Victim/js/app.js now serves the '{text}' bundle's content");
    }
}
