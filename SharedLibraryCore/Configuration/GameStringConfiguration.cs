using System.Collections.Generic;
using Humanizer;

namespace SharedLibraryCore.Configuration
{
    public class GameStringConfiguration : Dictionary<Server.Game, Dictionary<string, string>>
    {
        public string GetStringForGame(string key, Server.Game game = Server.Game.IW4)
        {
            if (key == null)
            {
                return null;
            }

            if (!ContainsKey(game))
            {
                return key.Transform(To.TitleCase);
            }

            var strings = this[game];
            return !strings.ContainsKey(key) ? key.Transform(To.TitleCase) : strings[key];
        }
    }
}