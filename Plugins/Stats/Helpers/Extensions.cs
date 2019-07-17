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

            return new Vector3(Y, X, vector.Z);
        }

        public static float ToRadians(this float value) => (float)Math.PI * value / 180.0f;

        public static float ToDegrees(this float value) => value * 180.0f / (float)Math.PI;

        public static double[] AngleStuff(Vector3 a, Vector3 b)
        {
            double deltaX = 180.0 - Math.Abs(Math.Abs(a.X - b.X) - 180.0);
            double deltaY = 180.0 - Math.Abs(Math.Abs(a.Y - b.Y) - 180.0);

            return new[] { deltaX, deltaY };
        }
    }
}
