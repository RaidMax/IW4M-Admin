using System;

namespace SharedLibraryCore.Helpers
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
    public class LinkedConfiguration : Attribute
    {
        public string[] LinkedPropertyNames { get; set; }

        public LinkedConfiguration(params string[] linkedPropertyNames)
        {
            LinkedPropertyNames = linkedPropertyNames;
        }
    }
}
