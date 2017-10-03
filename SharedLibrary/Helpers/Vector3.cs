using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SharedLibrary.Helpers
{
    public class Vector3
    {
        public float X { get; private set; }
        public  float Y { get; private set; }
        public float Z { get; private set; }

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
            bool valid = Regex.Match(s, @"\(-?[0-9]+.?[0-9]*,\ -?[0-9]+.?[0-9]*,\ -?[0-9]+.?[0-9]*\)").Success;
            if (!valid)
                throw new FormatException("Vector3 is not in correct format");

            string removeParenthesis = s.Substring(1, s.Length - 2);
            string[] eachPoint = removeParenthesis.Split(',');
            return new Vector3(float.Parse(eachPoint[0]), float.Parse(eachPoint[1]), float.Parse(eachPoint[2]));
        }

        public static double Distance(Vector3 a, Vector3 b)
        {
            return Math.Round(Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2) + Math.Pow(b.Z - a.Z, 2)), 2);
        }
    }
}
