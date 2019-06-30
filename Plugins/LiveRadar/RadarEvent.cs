using SharedLibraryCore;
using SharedLibraryCore.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        public string Weapon { get; set; }
        public int Health { get; set; }
        public bool IsAlive { get; set; }
        public Vector3 RadianAngles => new Vector3(ViewAngles.X.ToRadians(), ViewAngles.Y.ToRadians(), ViewAngles.Z.ToRadians());

        public static RadarEvent Parse(string input)
        {
            var items = input.Split(';').ToList();

            var parsedEvent = new RadarEvent()
            {
                Guid = items[0].ConvertLong(),
                Location = Vector3.Parse(items[1]),
                ViewAngles = Vector3.Parse(items[2]),
                Team = items[3],
                Kills = int.Parse(items[4]),
                Deaths = int.Parse(items[5]),
                Score = int.Parse(items[6]),
                Weapon = items[7],
                Health = int.Parse(items[8]),
                IsAlive = items[9] == "1"
            };

            return parsedEvent;
        }
    }
}
