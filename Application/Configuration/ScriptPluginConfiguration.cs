using System.Collections.Generic;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Configuration
{
    public class ScriptPluginConfiguration : Dictionary<string, Dictionary<string, object>>, IBaseConfiguration
    {
        public string Name() => nameof(ScriptPluginConfiguration);

        public IBaseConfiguration Generate()
        {
            return new ScriptPluginConfiguration();
        }
    }
}