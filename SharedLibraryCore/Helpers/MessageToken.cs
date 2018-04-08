using System;

namespace SharedLibraryCore.Helpers
{
    public class MessageToken
    {
        public string Name { get; private set; }
        Func<string> Value;
        public MessageToken(string Name, Func<string> Value)
        {
            this.Name = Name;
            this.Value = Value;
        }

        public override string ToString()
        {
            return Value().ToString();
        }
    }
}
