using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SharedLibrary.Helpers
{
    public class ConfigurationManager
    {
        ConcurrentDictionary<string, dynamic> ConfigSet;
        Server ServerInstance;

        public ConfigurationManager(Server S)
        {
            try
            {
                ConfigSet = Interfaces.Serialize<ConcurrentDictionary<string, dynamic>>.Read($"{Utilities.OperatingDirectory}config/plugins_{S.ToString()}.cfg");
            }

            catch (Exception)
            {
                S.Logger.WriteInfo("ConfigurationManager could not deserialize configuration file, so initializing default config set");
                ConfigSet = new ConcurrentDictionary<string, dynamic>();
            }

            ServerInstance = S;
            SaveChanges();
        }

        private void SaveChanges()
        {
            Interfaces.Serialize<ConcurrentDictionary<string, dynamic>>.Write($"{Utilities.OperatingDirectory}config/plugins_{ServerInstance.ToString()}.cfg", ConfigSet);
        }

        public void AddProperty(KeyValuePair<string, dynamic> prop)
        {
            if (!ConfigSet.ContainsKey(prop.Key))
                ConfigSet.TryAdd(prop.Key, prop.Value);

            SaveChanges();
        }

        public void UpdateProperty(KeyValuePair<string, dynamic> prop)
        {
            if (ConfigSet.ContainsKey(prop.Key))
                ConfigSet[prop.Key] = prop.Value;

            SaveChanges();
        }

        public T GetProperty<T>(string prop)
        {
            try
            {
                return ConfigSet[prop].ToObject<T>();
            }

            catch (RuntimeBinderException)
            {
                return ConfigSet[prop];
            }

            catch (Exception)
            {
                return default(T);
            }
        }
    }
}
