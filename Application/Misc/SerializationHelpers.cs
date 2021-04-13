using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using System;
using System.Net;
using Data.Models;
using static SharedLibraryCore.Database.Models.EFClient;
using static SharedLibraryCore.GameEvent;

namespace IW4MAdmin.Application.Misc
{
    class IPAddressConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(IPAddress));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return IPAddress.Parse((string)reader.Value);
        }
    }

    class IPEndPointConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(IPEndPoint));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            IPEndPoint ep = (IPEndPoint)value;
            JObject jo = new JObject();
            jo.Add("Address", JToken.FromObject(ep.Address, serializer));
            jo.Add("Port", ep.Port);
            jo.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);
            IPAddress address = jo["Address"].ToObject<IPAddress>(serializer);
            int port = (int)jo["Port"];
            return new IPEndPoint(address, port);
        }
    }

    class ClientEntityConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(EFClient);

        public override object ReadJson(JsonReader reader, Type objectType,object existingValue, JsonSerializer serializer)
        {
            if (reader.Value == null)
            {
                return null;
            }

            var jsonObject = JObject.Load(reader);

            return new EFClient
            {
                NetworkId = (long)jsonObject["NetworkId"],
                ClientNumber = (int)jsonObject["ClientNumber"],
                State = Enum.Parse<ClientState>(jsonObject["state"].ToString()),
                CurrentAlias = new EFAlias()
                {
                    IPAddress = (int?)jsonObject["IPAddress"],
                    Name = jsonObject["Name"].ToString()
                }
            };
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var client = value as EFClient;
            var jsonObject = new JObject
            {
                { "NetworkId", client.NetworkId },
                { "ClientNumber", client.ClientNumber },
                { "IPAddress", client.CurrentAlias?.IPAddress },
                { "Name", client.CurrentAlias?.Name },
                { "State", (int)client.State }
            };

            jsonObject.WriteTo(writer);
        }
    }

    class GameEventConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) =>objectType == typeof(GameEvent);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jsonObject = JObject.Load(reader);

            return new GameEvent
            {
                Type = Enum.Parse<EventType>(jsonObject["Type"].ToString()),
                Subtype = jsonObject["Subtype"]?.ToString(),
                Source = Enum.Parse<EventSource>(jsonObject["Source"].ToString()),
                RequiredEntity = Enum.Parse<EventRequiredEntity>(jsonObject["RequiredEntity"].ToString()),
                Data = jsonObject["Data"].ToString(),
                Message = jsonObject["Message"].ToString(),
                GameTime = (int?)jsonObject["GameTime"],
                Origin = jsonObject["Origin"]?.ToObject<EFClient>(serializer),
                Target = jsonObject["Target"]?.ToObject<EFClient>(serializer),
                ImpersonationOrigin = jsonObject["ImpersonationOrigin"]?.ToObject<EFClient>(serializer),
                IsRemote = (bool)jsonObject["IsRemote"],
                Extra = null, // fix
                Time = (DateTime)jsonObject["Time"],
                IsBlocking = (bool)jsonObject["IsBlocking"]
            };
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var gameEvent = value as GameEvent;

            var jsonObject = new JObject
            {
                { "Type", (int)gameEvent.Type },
                { "Subtype", gameEvent.Subtype },
                { "Source", (int)gameEvent.Source },
                { "RequiredEntity", (int)gameEvent.RequiredEntity },
                { "Data", gameEvent.Data },
                { "Message", gameEvent.Message },
                { "GameTime", gameEvent.GameTime },
                { "Origin", gameEvent.Origin != null ? JToken.FromObject(gameEvent.Origin, serializer) : null },
                { "Target", gameEvent.Target != null ? JToken.FromObject(gameEvent.Target, serializer) : null },
                { "ImpersonationOrigin", gameEvent.ImpersonationOrigin != null ? JToken.FromObject(gameEvent.ImpersonationOrigin, serializer) : null},
                { "IsRemote", gameEvent.IsRemote },
                { "Extra", gameEvent.Extra?.ToString() },
                { "Time", gameEvent.Time },
                { "IsBlocking", gameEvent.IsBlocking }
            };

            jsonObject.WriteTo(writer);
        }
    }
}
