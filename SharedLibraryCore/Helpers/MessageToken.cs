using System;

namespace SharedLibraryCore.Helpers
{
    public class MessageToken
    {
        public string Name { get; private set; }
        Func<Server, string> Value;
        public MessageToken(string Name, Func<Server, string> Value)
        {
            this.Name = Name;
            this.Value = Value;
        }
        
        public string Process(Server server)
        {
            return this.Value(server);
        }
    }
}
