using System;

namespace SharedLibraryCore.Configuration.Attributes
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
    public class ConfigurationLinked : Attribute
    {
        public string[] LinkedPropertyNames { get; set; }

        public ConfigurationLinked(params string[] linkedPropertyNames)
        {
            LinkedPropertyNames = linkedPropertyNames;
        }
    }
}
