using System;
using System.Threading;
using System.Threading.Tasks;

namespace SharedLibraryCore.Helpers
{
    public class MessageToken
    {
        public string Name { get; private set; }
        private readonly Func<Server, Task<object>> _asyncValue;


        public MessageToken(string Name, Func<Server, Task<object>> Value)
        {
            this.Name = Name;
            _asyncValue = Value;
        }

        public Task<object> ProcessAsync(Server server)
        {
            return _asyncValue(server);
        }
    }
}
