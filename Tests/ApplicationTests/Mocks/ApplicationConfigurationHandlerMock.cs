using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using System;
using System.Threading.Tasks;

namespace ApplicationTests.Mocks
{
    class ApplicationConfigurationHandlerMock : IConfigurationHandler<ApplicationConfiguration>
    {
        private readonly ApplicationConfiguration _appConfig;

        public ApplicationConfigurationHandlerMock(ApplicationConfiguration configuration)
        {
            _appConfig = configuration;
        }

        public string FileName => "";

        public void Build()
        {
            
        }

        public ApplicationConfiguration Configuration() => _appConfig;

        public Task Save()
        {
            throw new NotImplementedException();
        }

        public void Set(ApplicationConfiguration config)
        {
            throw new NotImplementedException();
        }
    }
}
