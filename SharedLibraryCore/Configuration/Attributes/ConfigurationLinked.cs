using System;

namespace SharedLibraryCore.Configuration.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ConfigurationLinked : Attribute
    {
        public ConfigurationLinked(params string[] linkedPropertyNames)
        {
            LinkedPropertyNames = linkedPropertyNames;
        }

        public string[] LinkedPropertyNames { get; set; }
    }
}