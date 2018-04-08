using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.Stats
{
    public class MinimapInfo
    {
        public string MapName { get; set; }
        // distance from the edge of the minimap image
        // to the "playable" area
        public int Top { get; set; }
        public int Bottom { get; set; }
        public int Left { get; set; }
        public int Right { get; set; }
        // maximum coordinate values for the map
        public int MaxTop { get; set; }
        public int MaxBottom { get; set; }
        public int MaxLeft { get; set; }
        public int MaxRight { get; set; }

        public int Width => MaxLeft - MaxRight;
        public int Height => MaxTop - MaxBottom;
    }

    public class MinimapConfig : Serialize<MinimapConfig>
    {
        public List<MinimapInfo> MapInfo;

        public static MinimapConfig IW4Minimaps()
        {
            return new MinimapConfig()
            {
                MapInfo = new List<MinimapInfo>()
                {
                    new MinimapInfo()
                    {
                        MapName = "mp_terminal",
                        Top = 85,
                        Bottom = 89,
                        Left = 7,
                        Right = 6,
                        MaxTop = 2929,
                        MaxBottom = -513,
                        MaxLeft = 7520,
                        MaxRight = 2447
                    },

                    new MinimapInfo()
                    {
                        MapName = "mp_rust",
                        Top = 122,
                        Bottom = 104, 
                        Left = 155,
                        Right = 82,
                        MaxRight = -225,
                        MaxLeft = 1809, 
                        MaxTop = 1641,
                        MaxBottom = -469
                    }
                }
            };
        }
    }
}
