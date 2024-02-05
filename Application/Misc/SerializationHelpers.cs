using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using System;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Data.Models;
using static SharedLibraryCore.Database.Models.EFClient;
using static SharedLibraryCore.GameEvent;

namespace IW4MAdmin.Application.Misc;

public class IPAddressConverter : JsonConverter<IPAddress>
{
    public override IPAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var ipAddressString = reader.GetString();
        return IPAddress.Parse(ipAddressString);
    }

    public override void Write(Utf8JsonWriter writer, IPAddress value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public class IPEndPointConverter : JsonConverter<IPEndPoint>
{
    public override IPEndPoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        IPAddress address = null;
        var port = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString();
                reader.Read();
                switch (propertyName)
                {
                    case "Address":
                        var addressString = reader.GetString();
                        address = IPAddress.Parse(addressString);
                        break;
                    case "Port":
                        port = reader.GetInt32();
                        break;
                }
            }

            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }
        }

        return new IPEndPoint(address, port);
    }

    public override void Write(Utf8JsonWriter writer, IPEndPoint value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Address", value.Address.ToString());
        writer.WriteNumber("Port", value.Port);
        writer.WriteEndObject();
    }
}

public class ClientEntityConverter : JsonConverter<EFClient>
{
    public override EFClient Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        long networkId = default;
        int clientNumber = default;
        ClientState state = default;
        var currentAlias = new EFAlias();
        int? ipAddress = null;
        string name = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString();
                reader.Read(); // Advance to the value.
                switch (propertyName)
                {
                    case "NetworkId":
                        networkId = reader.GetInt64();
                        break;
                    case "ClientNumber":
                        clientNumber = reader.GetInt32();
                        break;
                    case "State":
                        state = (ClientState)reader.GetInt32();
                        break;
                    case "IPAddress":
                        ipAddress = reader.TokenType != JsonTokenType.Null ? reader.GetInt32() : null;
                        break;
                    case "Name":
                        name = reader.GetString();
                        break;
                }
            }
        }

        currentAlias.IPAddress = ipAddress;
        currentAlias.Name = name;

        return new EFClient
        {
            NetworkId = networkId,
            ClientNumber = clientNumber,
            State = state,
            CurrentAlias = currentAlias
        };
    }

    public override void Write(Utf8JsonWriter writer, EFClient value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteNumber("NetworkId", value.NetworkId);
        writer.WriteNumber("ClientNumber", value.ClientNumber);
        writer.WriteString("State", value.State.ToString());

        if (value.CurrentAlias != null)
        {
            writer.WriteNumber("IPAddress", value.CurrentAlias.IPAddress ?? 0);
            writer.WriteString("Name", value.CurrentAlias.Name);
        }
        else
        {
            writer.WriteNull("IPAddress");
            writer.WriteNull("Name");
        }

        writer.WriteEndObject();
    }
}

public class GameEventConverter : JsonConverter<GameEvent>
{
    public override GameEvent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var gameEvent = new GameEvent();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString();
                reader.Read();
                switch (propertyName)
                {
                    case "Type":
                        gameEvent.Type = (EventType)reader.GetInt32();
                        break;
                    case "Subtype":
                        gameEvent.Subtype = reader.GetString();
                        break;
                    case "Source":
                        gameEvent.Source = (EventSource)reader.GetInt32();
                        break;
                    case "RequiredEntity":
                        gameEvent.RequiredEntity = (EventRequiredEntity)reader.GetInt32();
                        break;
                    case "Data":
                        gameEvent.Data = reader.GetString();
                        break;
                    case "Message":
                        gameEvent.Message = reader.GetString();
                        break;
                    case "GameTime":
                        gameEvent.GameTime = reader.TokenType != JsonTokenType.Null ? reader.GetInt32() : null;
                        break;
                    case "Origin":
                        gameEvent.Origin = JsonSerializer.Deserialize<EFClient>(ref reader, options);
                        break;
                    case "Target":
                        gameEvent.Target = JsonSerializer.Deserialize<EFClient>(ref reader, options);
                        break;
                    case "ImpersonationOrigin":
                        gameEvent.ImpersonationOrigin = JsonSerializer.Deserialize<EFClient>(ref reader, options);
                        break;
                    case "IsRemote":
                        gameEvent.IsRemote = reader.GetBoolean();
                        break;
                    case "Time":
                        gameEvent.Time = reader.GetDateTime();
                        break;
                    case "IsBlocking":
                        gameEvent.IsBlocking = reader.GetBoolean();
                        break;
                }
            }
        }

        return gameEvent;
    }

    public override void Write(Utf8JsonWriter writer, GameEvent value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteNumber("Type", (int)value.Type);
        writer.WriteString("Subtype", value.Subtype);
        writer.WriteNumber("Source", (int)value.Source);
        writer.WriteNumber("RequiredEntity", (int)value.RequiredEntity);
        writer.WriteString("Data", value.Data);
        writer.WriteString("Message", value.Message);
        if (value.GameTime.HasValue)
        {
            writer.WriteNumber("GameTime", value.GameTime.Value);
        }
        else
        {
            writer.WriteNull("GameTime");
        }

        if (value.Origin != null)
        {
            writer.WritePropertyName("Origin");
            JsonSerializer.Serialize(writer, value.Origin, options);
        }

        if (value.Target != null)
        {
            writer.WritePropertyName("Target");
            JsonSerializer.Serialize(writer, value.Target, options);
        }

        if (value.ImpersonationOrigin != null)
        {
            writer.WritePropertyName("ImpersonationOrigin");
            JsonSerializer.Serialize(writer, value.ImpersonationOrigin, options);
        }

        writer.WriteBoolean("IsRemote", value.IsRemote);
        writer.WriteString("Time", value.Time.ToString("o"));
        writer.WriteBoolean("IsBlocking", value.IsBlocking);

        writer.WriteEndObject();
    }
}
