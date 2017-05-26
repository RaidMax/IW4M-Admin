using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

using SharedLibrary.Interfaces;


namespace IW4MAdmin
{
    public class Config : Serialize<Config>
    {
        public string IP;
        public int Port;
        public string Password;
        public string FtpPrefix;

        public override string Filename()
        {
            return $"config/servers/{IP}_{Port}.cfg";
        }

        public static Config Generate()
        {
            string IP = String.Empty;
            int Port = 0;
            string Password;

            while(IP == String.Empty)
            {
                try
                {
                    Console.Write("Enter server IP: ");
                    string input = Console.ReadLine();
                    IPAddress.Parse(input);
                    IP = input;
                }

                catch (Exception)
                {
                    continue;
                }
            }

            while(Port == 0)
            {
                try
                {
                    Console.Write("Enter server port: ");
                    Port = Int32.Parse(Console.ReadLine());
                }

                catch (Exception)
                {
                    continue;
                }
            }

            Console.Write("Enter server RCON password: ");
            Password = Console.ReadLine();

            var config = new Config() { IP = IP, Password = Password, Port = Port };
            config.Write();

            Console.WriteLine("Config saved, add another? [y/n]:");
            if (Console.ReadLine().ToLower().First() == 'y')
                Generate();

            return config;
        }
    }
}
