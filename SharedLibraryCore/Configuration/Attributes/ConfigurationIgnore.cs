using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Configuration.Attributes
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
    public class ConfigurationIgnore : Attribute
    {
    }
}
