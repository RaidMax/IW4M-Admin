using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace LiveRadar.Configuration
{
    class LiveRadarConfiguration : IBaseConfiguration
    {
        public List<MapInfo> Maps { get; set; }

        public IBaseConfiguration Generate()
        {
            Maps = new List<MapInfo>()
            {
                new MapInfo()
                {
                    Name = "mp_afghan",
                    MaxLeft =  4600, // ymax
                    MaxRight =  -1100, // ymin
                    MaxBottom =  -1400, // xmin
                    MaxTop =  4600, // xmax
                    Left =  52, // pxmin
                    Right =  898, // pxmax
                    Bottom =  930, // yxmax
                    Top =  44 // pymin
                },
                new MapInfo()
                {
                    Name = "mp_rust",
                    Top = 212,
                    Bottom = 812,
                    Left = 314,
                    Right = 856,
                    MaxRight = -225,
                    MaxLeft = 1809,
                    MaxTop = 1773,
                    MaxBottom = -469
                },

                new MapInfo()
                {
                    Name = "mp_subbase",
                    MaxLeft =  1841, // ymax
                    MaxRight =  -3817, // ymin
                    MaxBottom =  -1585, // xmin
                    MaxTop =  2593, // xmax
                    Left =  18, // pxmin
                    Right =  968, // pxmax
                    Bottom =  864, // pymax
                    Top =  160, // pymin
                    CenterX = 0,
                    CenterY = 0,
                    Rotation = 0
                },

                new MapInfo()
                {
                    Name = "mp_estate",
                    Top = 52,
                    Bottom = 999,
                    Left= 173,
                    Right = 942,
                    MaxTop = 2103,
                    MaxBottom = -5077,
                    MaxLeft = 4437,
                    MaxRight = -1240,
                    Rotation = 143,
                    CenterX = -1440,
                    CenterY = 1920,
                    Scaler = 0.85f
                },

                new  MapInfo()
                {
                    Name = "mp_highrise",
                    MaxBottom = -3909,
                    MaxTop =  1649,
                    MaxRight = 5111,
                    MaxLeft = 8906,
                    Left =  108,
                    Right =  722,
                    Top = 66,
                    Bottom = 974,
                },

                new MapInfo()
                {
                    Name = "mp_quarry",
                    MaxBottom =  -5905,
                    MaxTop =  -1423,
                    MaxRight = -2095,
                    MaxLeft = 3217,
                    Left =  126,
                    Right =  968,
                    Top = 114,
                    Bottom = 824
                }
            };

            return this;
        }

        public string Name() => "LiveRadar";
    }
}
