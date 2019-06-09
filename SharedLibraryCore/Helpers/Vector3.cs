using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SharedLibraryCore.Helpers
{
    public class Vector3
    {
        [Key]
        public int Vector3Id { get; set; }
        public float X { get; protected set; }
        public  float Y { get; protected set; }
        public float Z { get; protected set; }

        // this is for EF and really should be somewhere else
        public Vector3()
        {

        }

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString()
        {
            return $"({X}, {Y}, {Z})";
        }

        public static Vector3 Parse(string s)
        {
            bool valid = Regex.Match(s, @"\((-?[0-9]+\.?[0-9]*|-?[0-9]+\.?[0-9]*e-[0-9]+),\ (-?[0-9]+\.?[0-9]*|-?[0-9]+\.?[0-9]*e-[0-9]+),\ (-?[0-9]+\.?[0-9]*|-?[0-9]+\.?[0-9]*e-[0-9]+)\)").Success;
            if (!valid)
            {
                throw new FormatException("Vector3 is not in correct format");
            }

            string removeParenthesis = s.Substring(1, s.Length - 2);
            string[] eachPoint = removeParenthesis.Split(',');

            return new Vector3(float.Parse(eachPoint[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture), 
                float.Parse(eachPoint[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture), 
                float.Parse(eachPoint[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture));
        }

        public static double Distance(Vector3 a, Vector3 b)
        {
            return Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2) + Math.Pow(b.Z - a.Z, 2));
        }

        public static double AbsoluteDistance(Vector3 a, Vector3 b)
        {
            double deltaX = Math.Abs(b.X -a.X);
            double deltaY = Math.Abs(b.Y - a.Y);

            // this 'fixes' the roll-over angles
            double dx = deltaX < 360.0 / 2 ? deltaX : 360.0 - deltaX;
            double dy = deltaY < 360.0 / 2 ? deltaY : 360.0 - deltaY;

            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        public static double ViewAngleDistance(Vector3 a, Vector3 b, Vector3 c)
        {
            double dabX = Math.Abs(a.X - b.X);
            dabX = dabX < 360.0 / 2 ? dabX : 360.0 - dabX;
            double dabY = Math.Abs(a.Y - b.Y);
            dabY = dabY < 360.0 / 2 ? dabY : 360.0 - dabY;

            double dacX = Math.Abs(a.X - c.X);
            dacX = dacX < 360.0 / 2 ? dacX : 360.0 - dacX;
            double dacY = Math.Abs(a.Y - c.Y);
            dacY = dacY < 360.0 / 2 ? dacY : 360.0 - dacY;

            double dbcX = Math.Abs(b.X - c.X);
            dbcX = dbcX < 360.0 / 2 ? dbcX : 360.0 - dbcX;
            double dbcY = Math.Abs(b.Y - c.Y);
            dbcY = dbcY < 360.0 / 2 ? dbcY : 360.0 - dbcY;

            double deltaX = (dabX - dacX - dbcX) / 2.0;
            deltaX = deltaX < 360.0 / 2 ? deltaX : 360.0 - deltaX;
            double deltaY = (dabY - dacY - dbcY) / 2.0;
            deltaY = deltaY < 360.0 / 2 ? deltaY : 360.0 - deltaY;

            return Math.Round(Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)), 4);
        }

        public static Vector3 Subtract(Vector3 a, Vector3 b) => new Vector3(b.X - a.X, b.Y - a.Y, b.Z - a.Z);

        public double DotProduct(Vector3 a) => (a.X * this.X) + (a.Y * this.Y) + (a.Z * this.Z);

        public double Magnitude() => Math.Sqrt((X * X) + (Y * Y) + (Z * Z));

        public double AngleBetween(Vector3 a) => Math.Acos(this.DotProduct(a) / (a.Magnitude() * this.Magnitude()));
    }
}
