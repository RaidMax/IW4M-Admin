using SharedLibrary.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsPlugin.Helpers
{
    static class Extensions
    {

        public static Vector3 FixIW4Angles(this Vector3 vector)
        {
            float X = vector.X > 0 ? 360.0f - vector.X : Math.Abs(vector.X);
            float Y = vector.Y > 0 ? 360.0f - vector.Y : Math.Abs(vector.Y);
            float Z = vector.Z > 0 ? 360.0f - vector.Z : Math.Abs(vector.Z);

            return new Vector3(X, Y, Z);
        }

        public static float ToRadians(this float value) => (float)Math.PI * value / 180.0f;

        public static float ToDegrees(this float value) => value * 180.0f / (float)Math.PI;
    }
}
