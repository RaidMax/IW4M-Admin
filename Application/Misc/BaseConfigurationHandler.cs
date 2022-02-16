using SharedLibraryCore;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Interfaces;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace IW4MAdmin.Application.Misc
{
    /// <summary>
    /// default implementation of IConfigurationHandler
    /// </summary>
    /// <typeparam name="T">base configuration type</typeparam>
    public class BaseConfigurationHandler<T> : IConfigurationHandler<T> where T : IBaseConfiguration
    {
        private T _configuration;
        private readonly SemaphoreSlim _onSaving;
        private readonly JsonSerializerOptions _serializerOptions;


        public BaseConfigurationHandler(string fileName)
        {
            _serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
            };
            _serializerOptions.Converters.Add(new JsonStringEnumConverter());
            _onSaving = new SemaphoreSlim(1, 1);
            FileName = Path.Join(Utilities.OperatingDirectory, "Configuration", $"{fileName}.json");
        }

        public BaseConfigurationHandler() : this(typeof(T).Name)
        {
        }

        ~BaseConfigurationHandler()
        {
            _onSaving.Dispose();
        }

        public string FileName { get; }

        public async Task BuildAsync()
        {
            try
            {
                await using var fileStream = File.OpenRead(FileName);
                _configuration = await JsonSerializer.DeserializeAsync<T>(fileStream, _serializerOptions);
            }

            catch (FileNotFoundException)
            {
                _configuration = default;
            }

            catch (Exception e)
            {
                throw new ConfigurationException("Could not load configuration")
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

                await using var fileStream = File.Create(FileName);
                await JsonSerializer.SerializeAsync(fileStream, _configuration, _serializerOptions);
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
