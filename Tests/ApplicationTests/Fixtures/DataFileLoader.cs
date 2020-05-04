using IW4MAdmin.Application.Misc;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;

namespace ApplicationTests.Fixtures
{
    class DataFileLoader
    {
        public async Task<T> Load<T>(string fileName)
        {
            string data = await File.ReadAllTextAsync($"{fileName}.json");
            return JsonConvert.DeserializeObject<T>(data, EventLog.BuildVcrSerializationSettings());
        }
    }
}
