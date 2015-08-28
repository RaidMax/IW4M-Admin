using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SharedLibrary
{
    public struct dvar
    {
        public String name;
        public String description;
        public int flags;
        public short type;
        public String current;
        public String latched;
        public String _default;
        public int min;
        public int max;
    } 
}
