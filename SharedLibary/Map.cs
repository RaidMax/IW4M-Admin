using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
    }
}
