using System;
using System.Linq;

/*https://stackoverflow.com/questions/26581572/how-to-convert-string-into-version-in-net-3-5*/
namespace SharedLibraryCore.Helpers
{
    public class BuildNumber : IComparable
    {
        private BuildNumber()
        {
        }

        public int Major { get; private set; }
        public int Minor { get; private set; }
        public int Build { get; private set; }
        public int Revision { get; private set; }

        public int CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            var buildNumber = obj as BuildNumber;
            if (buildNumber == null)
            {
                return 1;
            }

            if (ReferenceEquals(this, buildNumber))
            {
                return 0;
            }

            return Major == buildNumber.Major
                ? Minor == buildNumber.Minor
                    ? Build == buildNumber.Build
                        ? Revision.CompareTo(buildNumber.Revision)
                        : Build.CompareTo(buildNumber.Build)
                    : Minor.CompareTo(buildNumber.Minor)
                : Major.CompareTo(buildNumber.Major);
        }

        public static bool TryParse(string input, out BuildNumber buildNumber)
        {
            try
            {
                buildNumber = Parse(input);
                return true;
            }
            catch
            {
                buildNumber = null;
                return false;
            }
        }

        /// <summary>
        ///     Parses a build number string into a BuildNumber class
        /// </summary>
        /// <param name="buildNumber">The build number string to parse</param>
        /// <returns>A new BuildNumber class set from the buildNumber string</returns>
        /// <exception cref="ArgumentException">
        ///     Thrown if there are less than 2 or
        ///     more than 4 version parts to the build number
        /// </exception>
        /// <exception cref="FormatException">
        ///     Thrown if string cannot be parsed
        ///     to a series of integers
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown if any version
        ///     integer is less than zero
        /// </exception>
        public static BuildNumber Parse(string buildNumber)
        {
            if (buildNumber == null)
            {
                throw new ArgumentNullException("buildNumber");
            }

            var versions = buildNumber
                .Split(new[] { '.' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .ToList();

            if (versions.Count < 2)
            {
                throw new ArgumentException("BuildNumber string was too short");
            }

            if (versions.Count > 4)
            {
                throw new ArgumentException("BuildNumber string was too long");
            }

            return new BuildNumber
            {
                Major = ParseVersion(versions[0]),
                Minor = ParseVersion(versions[1]),
                Build = versions.Count > 2 ? ParseVersion(versions[2]) : -1,
                Revision = versions.Count > 3 ? ParseVersion(versions[3]) : -1
            };
        }

        private static int ParseVersion(string input)
        {
            int version;

            if (!int.TryParse(input, out version))
            {
                throw new FormatException(
                    "buildNumber string was not in a correct format");
            }

            if (version < 0)
            {
                throw new ArgumentOutOfRangeException(
                    "buildNumber",
                    "Versions must be greater than or equal to zero");
            }

            return version;
        }

        public override string ToString()
        {
            return string.Format("{0}.{1}{2}{3}", Major, Minor,
                Build < 0 ? "" : "." + Build,
                Revision < 0 ? "" : "." + Revision);
        }

        public static bool operator >(BuildNumber first, BuildNumber second)
        {
            return first.CompareTo(second) > 0;
        }

        public static bool operator <(BuildNumber first, BuildNumber second)
        {
            return first.CompareTo(second) < 0;
        }

        public override bool Equals(object obj)
        {
            return CompareTo(obj) == 0;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + Major.GetHashCode();
                hash = hash * 23 + Minor.GetHashCode();
                hash = hash * 23 + Build.GetHashCode();
                hash = hash * 23 + Revision.GetHashCode();
                return hash;
            }
        }
    }
}