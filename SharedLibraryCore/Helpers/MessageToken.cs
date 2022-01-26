using System;
using System.Threading.Tasks;

namespace SharedLibraryCore.Helpers
{
    public class MessageToken
    {
        private readonly Func<Server, Task<string>> _asyncValue;


        public MessageToken(string Name, Func<Server, Task<string>> Value)
        {
            this.Name = Name;
            _asyncValue = Value;
        }

        public string Name { get; }

        public async Task<string> ProcessAsync(Server server)
        {
            var result = await _asyncValue(server);
            return result;
        }
    }
}