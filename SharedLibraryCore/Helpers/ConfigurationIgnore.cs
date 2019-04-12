using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Helpers
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
    public class ConfigurationIgnore : Attribute
    {
    }
}
