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

            var newConfig = new ServerConfiguration();

            while (string.IsNullOrEmpty(newConfig.IPAddress))
            {
                try
                {
                    Console.Write("Enter server IP Address: ");
                    string input = Console.ReadLine();
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
                    Console.Write("Enter server port: ");
                    newConfig.Port = Int16.Parse(Console.ReadLine());
                }

                catch (Exception)
                {
                    continue;
                }
            }

            Console.Write("Enter server RCON password: ");
            newConfig.Password = Console.ReadLine();
            newConfig.AutoMessages = new List<string>();
            newConfig.Rules = new List<string>();

            newConfig.UseT6MParser = Utilities.PromptBool("Use T6M parser");

            configList.Add(newConfig);

            Console.Write("Configuration saved, add another? [y/n]:");
            if (Console.ReadLine().ToLower().First() == 'y')
                GenerateServerConfig(configList);

            return configList;
        }
    }
}
