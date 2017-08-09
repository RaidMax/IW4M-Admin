using System;
using System.Collections.Generic;

namespace SharedLibrary.Helpers
{
    public class ConfigurationManager
    {
        Dictionary<string, Dictionary<string, object>> ConfigurationSet;
        Type PluginType;

        public ConfigurationManager(Type PluginType)
        {
            ConfigurationSet = new Dictionary<string, Dictionary<string, object>>();
            this.PluginType = PluginType;
        }

        public void AddConfiguration(Server S)
        {
            try
            {
                var Config = Interfaces.Serialize<Dictionary<string, object>>.Read($"config/{PluginType.ToString()}_{S.ToString()}.cfg");
                lock (ConfigurationSet)
                {
                    ConfigurationSet.Add(S.ToString(), Config);
                }
            }

            catch (Exceptions.SerializeException)
            {
                ConfigurationSet.Add(S.ToString(), new Dictionary<string, object>());
            }

        }

        public void AddProperty(Server S, KeyValuePair<string, object> Property)
        {
            ConfigurationSet[S.ToString()].Add(Property.Key, Property.Value);
            Interfaces.Serialize<Dictionary<string, object>>.Write($"config/{PluginType.ToString()}_{S.ToString()}.cfg", ConfigurationSet[S.ToString()]);
        }

        public void UpdateProperty(Server S, KeyValuePair<string, object> Property)
        {
            ConfigurationSet[S.ToString()][Property.Key] = Property.Value;
            Interfaces.Serialize<Dictionary<string, object>>.Write($"config/{PluginType.ToString()}_{S.ToString()}.cfg", ConfigurationSet[S.ToString()]);
        }

        public IDictionary<string, object> GetConfiguration(Server S)
        {
            return ConfigurationSet[S.ToString()];
        }
    }
}
