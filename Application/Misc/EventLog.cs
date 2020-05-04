using Newtonsoft.Json;
using SharedLibraryCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace IW4MAdmin.Application.Misc
{
    public class EventLog : Dictionary<long, IList<GameEvent>>
    {
        private static JsonSerializerSettings serializationSettings;

        public static JsonSerializerSettings BuildVcrSerializationSettings()
        {
            if (serializationSettings == null)
            {
                serializationSettings = new JsonSerializerSettings() { Formatting = Formatting.Indented, ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
                serializationSettings.Converters.Add(new IPAddressConverter());
                serializationSettings.Converters.Add(new IPEndPointConverter());
                serializationSettings.Converters.Add(new GameEventConverter());
                serializationSettings.Converters.Add(new ClientEntityConverter());
            }

            return serializationSettings;
        }
    }
}
