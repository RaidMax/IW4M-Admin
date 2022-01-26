using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Configuration.Extensions
{
    public static class ConfigurationExtensions
    {
        public static void TrySetIpAddress(this ServerConfiguration config)
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces().Where(nic =>
                    nic.OperationalStatus == OperationalStatus.Up &&
                    (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                     nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet && nic.GetIPProperties().UnicastAddresses
                         .Any(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork))).ToList();

                var publicInterfaces = interfaces.Where(nic =>
                        nic.GetIPProperties().UnicastAddresses.Any(info =>
                            info.Address.AddressFamily == AddressFamily.InterNetwork && !info.Address.IsInternal()))
                    .ToList();

                config.IPAddress = publicInterfaces.Any()
                    ? publicInterfaces.First().GetIPProperties().UnicastAddresses.First().Address.ToString()
                    : IPAddress.Loopback.ToString();
            }
            catch
            {
                config.IPAddress = IPAddress.Loopback.ToString();
            }
        }

        public static (string, string)[] TryGetRConPasswords(this IRConParser parser)
        {
            string searchPath = null;
            var isRegistryKey = parser.Configuration.DefaultInstallationDirectoryHint.Contains("HKEY_");

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return null;
            }

            try
            {
                if (isRegistryKey)
                {
                    var result = Registry.GetValue(parser.Configuration.DefaultInstallationDirectoryHint, null, null);

                    if (result == null)
                    {
                        return Array.Empty<(string, string)>();
                    }

                    searchPath = Path.Combine(result.ToString()!.Split(Path.DirectorySeparatorChar)
                        .Where(p => !p.Contains(".exe"))
                        .Select(p => p.Replace("\"", "")).ToArray());
                }

                else
                {
                    var path = parser.Configuration.DefaultInstallationDirectoryHint.Replace("{LocalAppData}",
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

                    if (Directory.Exists(path))
                    {
                        searchPath = path;
                    }
                }

                if (string.IsNullOrEmpty(searchPath))
                {
                    return Array.Empty<(string, string)>();
                }

                var possibleFiles = Directory.GetFiles(searchPath, "*.cfg", SearchOption.AllDirectories);

                if (!possibleFiles.Any())
                {
                    return Array.Empty<(string, string)>();
                }

                var possiblePasswords = possibleFiles.SelectMany(File.ReadAllLines)
                    .Select(line => Regex.Match(line, "^(\\/\\/)?.*rcon_password +\"?([^\\/\"\n]+)\"?"))
                    .Where(match => match.Success)
                    .Select(match =>
                        !string.IsNullOrEmpty(match.Groups[1].ToString())
                            ? (match.Groups[2].ToString(),
                                Utilities.CurrentLocalization.LocalizationIndex["SETUP_RCON_PASSWORD_COMMENTED"])
                            : (match.Groups[2].ToString(), null));

                return possiblePasswords.ToArray();
            }
            catch
            {
                return Array.Empty<(string, string)>();
            }
        }
    }
}
