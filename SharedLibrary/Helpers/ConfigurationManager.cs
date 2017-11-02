using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SharedLibrary.Helpers
{
    public class ConfigurationManager
    {
        ConcurrentDictionary<string, Dictionary<string, object>> ConfigurationSet;
        ConcurrentDictionary<string, object> ConfigSet;
        Type PluginType;
        Server ServerInstance;

        public ConfigurationManager(Type PluginType)
        {
            ConfigurationSet = new ConcurrentDictionary<string, Dictionary<string, object>>();
            this.PluginType = PluginType;
        }

        public ConfigurationManager(Server S)
        {
            try
            {
                ConfigSet = Interfaces.Serialize<ConcurrentDictionary<string, object>>.Read($"config/Plugins_{S}.cfg");
            }

            catch (Exception)
            {
                S.Logger.WriteInfo("ConfigurationManager could not deserialize configuration file, so initializing default config set");
                ConfigSet = new ConcurrentDictionary<string, object>();
            }

            ServerInstance = S;
        }

        private void SaveChanges()
        {
            Interfaces.Serialize<ConcurrentDictionary<string, object>>.Write($"config/Plugins_{ServerInstance}.cfg", ConfigSet);
        }

        public void AddConfiguration(Server S)
        {
           /* if (ConfigurationSet.ContainsKey(S.ToString()))
            {
                S.Logger.WriteWarning($"not adding server configuration for {S} as it already exists");
                return;
            }*/

            try
            {
                var Config = Interfaces.Serialize<Dictionary<string, object>>.Read($"config/{PluginType.ToString()}_{S.ToString()}.cfg");
                ConfigurationSet.TryAdd(S.ToString(), Config);
            }

            catch (Exceptions.SerializeException)
            {
                ConfigurationSet.TryAdd(S.ToString(), new Dictionary<string, object>());
            }
        }

        public void AddProperty(Server S, KeyValuePair<string, object> Property)
        {
            ConfigurationSet[S.ToString()].Add(Property.Key, Property.Value);
            Interfaces.Serialize<Dictionary<string, object>>.Write($"config/{PluginType.ToString()}_{S.ToString()}.cfg", ConfigurationSet[S.ToString()]);
        }

        public  void AddProperty(KeyValuePair<string, object> prop)
        {
            if (!ConfigSet.ContainsKey(prop.Key))
                ConfigSet.TryAdd(prop.Key, prop.Value);

            SaveChanges();
        }

        public void UpdateProperty(Server S, KeyValuePair<string, object> Property)
        {
            ConfigurationSet[S.ToString()][Property.Key] = Property.Value;
            Interfaces.Serialize<Dictionary<string, object>>.Write($"config/{PluginType.ToString()}_{S.ToString()}.cfg", ConfigurationSet[S.ToString()]);
        }

        public void UpdateProperty(KeyValuePair<string, object> prop)
        {
            if (ConfigSet.ContainsKey(prop.Key))
                ConfigSet[prop.Key] = prop.Value;

            SaveChanges();
        }

        public IDictionary<string, object> GetConfiguration(Server S)
        {
            return ConfigurationSet[S.ToString()];
        }
        
        public object GetProperty(string prop)
        {
            try
            {
                return ConfigSet[prop];
            }

            catch (Exception)
            {
                return null;
            }
        }
    }
}
