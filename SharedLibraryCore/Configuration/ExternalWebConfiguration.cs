using System;
using SharedLibraryCore.Configuration.Attributes;

namespace SharedLibraryCore.Configuration;

public sealed class ExternalWebConfiguration
{
    public Uri Url { get; set; }

    [ConfigurationOptional]
    public string TokenName { get; set; }

    [ConfigurationOptional]
    public string TokenFile { get; set; }
}
