using System;
using System.Reflection;

namespace SharedLibraryCore
{
    public class Map
    {
        public String Name { get; set; }
        public String Alias { get; set; }

        public override string ToString() => Alias;
    }
}
