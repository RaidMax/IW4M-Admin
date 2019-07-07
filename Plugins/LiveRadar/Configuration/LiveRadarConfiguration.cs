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
                    MaxBottom = -469,
                    ViewPositionRotation = 180
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
                    CenterY = 0
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
                    Scaler = 0.85f,
                    ViewPositionRotation = 180
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
                },

                new MapInfo()
                {
                    Name = "mp_boneyard",
                    MaxBottom =  -1756,
                    MaxTop =  2345,
                    MaxRight = -715,
                    MaxLeft = 1664,
                    Left =  248,
                    Right =  728,
                    Top = 68,
                    Bottom = 897
                },

                new MapInfo()
                {
                    Name = "mp_brecourt",
                    MaxBottom =  -3797,
                    MaxTop =  4240,
                    MaxRight = -3876,
                    MaxLeft = 2575,
                    Left =  240,
                    Right =  846,
                    Top = 180,
                    Bottom = 934
                },

                new MapInfo()
                {
                    Name = "mp_checkpoint",
                    MaxBottom =  -2273,
                    MaxTop =  2153,
                    MaxRight = -3457,
                    MaxLeft = 2329,
                    Left =  30,
                    Right =  1010,
                    Top = 136,
                    Bottom = 890
                },

                new MapInfo()
                {
                    Name = "mp_derail",
                    MaxBottom =  -2775,
                    MaxTop =  3886,
                    MaxRight = -3807,
                    MaxLeft = 4490,
                    Left =  130,
                    Right =  892,
                    Top = 210,
                    Bottom = 829,
                    ViewPositionRotation = 180
                },

                new MapInfo()
                {
                    Name = "mp_favela",
                    MaxBottom =  -2017,
                    MaxTop =  1769,
                    MaxRight = -1239,
                    MaxLeft = 2998,
                    Left =  120,
                    Right =  912,
                    Top = 174,
                    Bottom = 878
                },

                new MapInfo()
                {
                    Name = "mp_invasion",
                    MaxBottom =  -3673,
                    MaxTop =  2540,
                    MaxRight = -3835,
                    MaxLeft = 980,
                    Left =  20,
                    Right =  808,
                    Top = 0,
                    Bottom = 1006
                },

                new MapInfo()
                {
                    Name = "mp_nightshift",
                    MaxBottom =  -2497,
                    MaxTop =  1977,
                    MaxRight = -2265,
                    MaxLeft = 945,
                    Left =  246,
                    Right = 826,
                    Top = 104,
                    Bottom = 916
                },

                new MapInfo()
                {
                    Name = "mp_rundown",
                    MaxBottom = -2304,
                    MaxTop =  3194,
                    MaxRight = -3558,
                    MaxLeft = 3361,
                    Left =  32,
                    Right =  1030,
                    Top = 96,
                    Bottom = 892
                },

                new MapInfo()
                {
                    Name = "mp_underpass",
                    MaxBottom =  -601,
                    MaxTop =  3761,
                    MaxRight = -1569,
                    MaxLeft = 3615,
                    Left =  42,
                    Right =  978,
                    Top = 157,
                    Bottom = 944
                }
            };

            return this;
        }

        public string Name() => "LiveRadar";
    }
}
