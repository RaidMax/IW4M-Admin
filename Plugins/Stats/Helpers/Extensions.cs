using SharedLibraryCore.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IW4MAdmin.Plugins.Stats.Helpers
{
    static class Extensions
    {

        public static Vector3 FixIW4Angles(this Vector3 vector)
        {
            float X = vector.X >= 0 ? vector.X : 360.0f + vector.X;
            float Y = vector.Y >= 0 ? vector.Y : 360.0f + vector.Y;
            float Z = vector.Z >= 0 ? vector.Z : 360.0f + vector.Z;

            return new Vector3(Y, X, Z);
        }

        public static float ToRadians(this float value) => (float)Math.PI * value / 180.0f;

        public static float ToDegrees(this float value) => value * 180.0f / (float)Math.PI;
    }
}
