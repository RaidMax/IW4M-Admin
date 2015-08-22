using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharedLibrary
{
    public abstract class Plugin
    {
        public abstract void onLoad();
    }

    public abstract class Notify : Plugin
    {
        public abstract void onEvent(Event E);
    }
}
