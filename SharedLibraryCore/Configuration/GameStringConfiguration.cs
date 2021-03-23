using System.Collections.Generic;
using Data.Models;
using Humanizer;

namespace SharedLibraryCore.Configuration
{
    public class GameStringConfiguration : Dictionary<Reference.Game, Dictionary<string, string>>
    {
        public string GetStringForGame(string key, Reference.Game? game = Reference.Game.IW4)
        {
            if (key == null)
            {
                return null;
            }

            if (!ContainsKey(game.Value))
            {
                return key.Transform(To.TitleCase);
            }

            var strings = this[game.Value];
            return !strings.ContainsKey(key) ? key.Transform(To.TitleCase) : strings[key];
        }
    }
}