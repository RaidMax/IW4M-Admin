using IW4MAdmin.Plugins.Stats.Client.Game;
using SharedLibraryCore;

namespace Stats.Client.Abstractions
{
    public interface IHitInfoBuilder
    {
        HitInfo Build(string[] log, int entityId, bool isSelf, bool isVictim, Server.Game gameName);
    }
}