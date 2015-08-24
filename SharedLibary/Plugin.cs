using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharedLibrary
{
    public abstract class Plugin
    {
        public abstract void onLoad();
        public abstract void onUnload();
        public abstract void onEvent(Event E);

        //for logging purposes
        public abstract String Name { get; }
        public abstract float Version { get; }  
        public abstract String Author { get; }
    }
}
