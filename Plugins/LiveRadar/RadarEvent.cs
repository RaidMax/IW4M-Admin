﻿using Data.Models;
using SharedLibraryCore;
using System;
using System.Linq;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace LiveRadar
{
    public class RadarEvent
    {
        public string Name { get; set; }
        public long Guid { get; set; }
        public Vector3 Location { get; set; }
        public Vector3 ViewAngles { get; set; }
        public string Team { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Score { get; set; }
        public int PlayTime { get; set; }
        public string Weapon { get; set; }
        public int Health { get; set; }
        public bool IsAlive { get; set; }
        public Vector3 RadianAngles => new Vector3(ViewAngles.X.ToRadians(), ViewAngles.Y.ToRadians(), ViewAngles.Z.ToRadians());
        public int Id => GetHashCode();

#pragma warning disable CS0659
        public override bool Equals(object obj)
#pragma warning restore CS0659
        {
            if (obj is RadarEvent re)
            {
                return re.ViewAngles.X == ViewAngles.X &&
                    re.ViewAngles.Y == ViewAngles.Y &&
                    re.ViewAngles.Z == ViewAngles.Z &&
                    re.Location.X == Location.X &&
                    re.Location.Y == Location.Y &&
                    re.Location.Z == Location.Z;
            }

            return false;
        }

        public static RadarEvent Parse(string input, long generatedBotGuid)
        {
            var items = input.Split(';').Skip(1).ToList();

            var parsedEvent = new RadarEvent()
            {
                Guid = generatedBotGuid,
                Location = Vector3.Parse(items[1]),
                ViewAngles = Vector3.Parse(items[2]).FixIW4Angles(),
                Team = items[3],
                Kills = int.Parse(items[4]),
                Deaths = int.Parse(items[5]),
                Score = int.Parse(items[6]),
                Weapon = items[7],
                Health = int.Parse(items[8]),
                IsAlive = items[9] == "1",
                PlayTime = Convert.ToInt32(items[10])
            };

            return parsedEvent;
        }
    }
}
