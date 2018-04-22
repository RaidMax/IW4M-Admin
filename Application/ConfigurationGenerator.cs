using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace IW4MAdmin.Application
{
    class ConfigurationGenerator
    {
        public static List<ServerConfiguration> GenerateServerConfig(List<ServerConfiguration> configList)
        {

            var loc = Utilities.CurrentLocalization.LocalizationSet;
            var newConfig = new ServerConfiguration();

            while (string.IsNullOrEmpty(newConfig.IPAddress))
            {
                try
                {
                    string input = Utilities.PromptString(loc["SETUP_SERVER_IP"]);
                    IPAddress.Parse(input);
                    newConfig.IPAddress = input;
                }

                catch (Exception)
                {
                    continue;
                }
            }

            while (newConfig.Port == 0)
            {
                try
                {
                    newConfig.Port = Int16.Parse(Utilities.PromptString(loc["SETUP_SERVER_PORT"]));
                }

                catch (Exception)
                {
                    continue;
                }
            }

            newConfig.Password = Utilities.PromptString(loc["SETUP_SERVER_RCON"]);
            newConfig.AutoMessages = new List<string>();
            newConfig.Rules = new List<string>();

            newConfig.UseT6MParser = Utilities.PromptBool(loc["SETUP_SERVER_USET6M"]);

            configList.Add(newConfig);

            if (Utilities.PromptBool(loc["SETUP_SERVER_SAVE"]))
                GenerateServerConfig(configList);

            return configList;
        }
    }
}
