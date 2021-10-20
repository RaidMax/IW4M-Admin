using IW4MAdmin.Plugins.Stats.Client.Game;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

namespace Stats.Client.Abstractions
{
    public interface IHitInfoBuilder
    {
        HitInfo Build(string[] log, ParserRegex parserRegex, int entityId, bool isSelf, bool isVictim, Server.Game gameName);
    }
}