using System;
using System.Threading;
using System.Threading.Tasks;

namespace SharedLibraryCore.Helpers
{
    public class MessageToken
    {
        public string Name { get; private set; }
        private readonly Func<Server, Task<string>> _asyncValue;


        public MessageToken(string Name, Func<Server, Task<string>> Value)
        {
            this.Name = Name;
            _asyncValue = Value;
        }

        public async Task<string> ProcessAsync(Server server)
        {
            string result = await _asyncValue(server);
            return result;
        }
    }
}
