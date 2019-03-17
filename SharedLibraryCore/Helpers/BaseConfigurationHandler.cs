using Newtonsoft.Json;
using SharedLibraryCore.Interfaces;
using System.IO;
using System.Threading.Tasks;

namespace SharedLibraryCore.Configuration
{
    public class BaseConfigurationHandler<T> : IConfigurationHandler<T> where T : IBaseConfiguration
    {
        readonly string _configurationPath;
        T _configuration;

        public BaseConfigurationHandler(string fn)
        {
            _configurationPath = Path.Join(Utilities.OperatingDirectory, "Configuration", $"{fn}.json");
            Build();
        }

        public void Build()
        {
            var configContent = File.ReadAllText(_configurationPath);
            _configuration = JsonConvert.DeserializeObject<T>(configContent);

            if (_configuration == null)
            {
                _configuration = default(T);
            }
        }

        public Task Save()
        {
            var appConfigJSON = JsonConvert.SerializeObject(_configuration, Formatting.Indented);
            return File.WriteAllTextAsync(_configurationPath, appConfigJSON);
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
