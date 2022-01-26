using Newtonsoft.Json;
using SharedLibraryCore;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Interfaces;
using System;
using System.IO;
using System.Threading;
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
        private readonly SemaphoreSlim _onSaving;

        public BaseConfigurationHandler(string fn)
        {
            _onSaving = new SemaphoreSlim(1, 1);
            FileName = Path.Join(Utilities.OperatingDirectory, "Configuration", $"{fn}.json");
            Build();
        }

        public BaseConfigurationHandler() : this(typeof(T).Name)
        {
            _onSaving = new SemaphoreSlim(1, 1);
        }

        ~BaseConfigurationHandler()
        {
            _onSaving.Dispose();
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

        public async Task Save()
        {
            try
            {
                await _onSaving.WaitAsync();
                var settings = new JsonSerializerSettings()
                {
                    Formatting = Formatting.Indented
                };
                settings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());

                var appConfigJson = JsonConvert.SerializeObject(_configuration, settings);
                await File.WriteAllTextAsync(FileName, appConfigJson);
            }

            finally
            {
                if (_onSaving.CurrentCount == 0)
                {
                    _onSaving.Release(1);
                }
            }
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
