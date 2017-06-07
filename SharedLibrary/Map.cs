using System;

namespace SharedLibrary
{
    public class Map
    {
        public Map(String N, String A)
        {
            Name = N;
            Alias = A;
        }

        public String Name { get; private set; }
        public String Alias { get; private set; }

        public override string ToString()
        {
            return Alias;
        }
    }
}
