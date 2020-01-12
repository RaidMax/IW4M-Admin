using Newtonsoft.Json;
using System;

namespace SharedLibraryCore.Helpers
{
    /// <summary>
    /// JSON converter for the build number
    /// </summary>
    public class BuildNumberJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return BuildNumber.Parse(reader.Value.ToString());
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }
    }
}
