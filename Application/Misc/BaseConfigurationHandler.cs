using Newtonsoft.Json;
using SharedLibraryCore;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;

namespace IW4MAdmin.Application.Misc
{
    /// <summary>
    /// default implementation of IConfigurationHandler
    /// </summary>
    /// <typeparam name="T">base configuration type</typeparam>
    public class BaseConfigurationHandler<T> : IConfigurationHandler<T> where T : IBaseConfiguration
    {
        T _configuration;

        public BaseConfigurationHandler(string fn)
        {
            FileName = Path.Join(Utilities.OperatingDirectory, "Configuration", $"{fn}.json");
            Build();
        }

        public string FileName { get; }

        public void Build()
        {
            try
            {
                var configContent = File.ReadAllText(FileName);
                _configuration = JsonConvert.DeserializeObject<T>(configContent);
            }

            catch (FileNotFoundException)
            {
                _configuration = default;
            }

            catch (Exception e)
            {
                throw new ConfigurationException("MANAGER_CONFIGURATION_ERROR")
                {
                    Errors = new[] { e.Message },
                    ConfigurationFileName = FileName
                };
            }
        }

        public Task Save()
        {
            var settings = new JsonSerializerSettings()
            {
                Formatting = Formatting.Indented
            };
            settings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());

            var appConfigJSON = JsonConvert.SerializeObject(_configuration, settings);
            return File.WriteAllTextAsync(FileName, appConfigJSON);
        }

        public T Configuration()
        {
            return _configuration;
        }

        public void Set(T config)
        {
            _configuration = config;
        }
    }
}
