using System;

namespace SharedLibraryCore.Configuration.Attributes
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
    public class ConfiguratinLinked : Attribute
    {
        public string[] LinkedPropertyNames { get; set; }

        public ConfiguratinLinked(params string[] linkedPropertyNames)
        {
            LinkedPropertyNames = linkedPropertyNames;
        }
    }
}
