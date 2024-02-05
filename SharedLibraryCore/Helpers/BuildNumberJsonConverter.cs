using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharedLibraryCore.Helpers;

/// <summary>
///     JSON converter for the build number
/// </summary>
public class BuildNumberJsonConverter : JsonConverter<BuildNumber>
{
    public override BuildNumber Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var stringValue = reader.GetString();
        return BuildNumber.Parse(stringValue);
    }

    public override void Write(Utf8JsonWriter writer, BuildNumber value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public class GameArrayJsonConverter : JsonConverter<Server.Game[]>
{
    public override Server.Game[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        List<Server.Game> games = [];

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            var gameString = reader.GetString();
            var game = Enum.Parse<Server.Game>(gameString);
            games.Add(game);
        }

        return games.ToArray();
    }

    public override void Write(Utf8JsonWriter writer, Server.Game[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var game in value)
        {
            writer.WriteStringValue(game.ToString());
        }

        writer.WriteEndArray();
    }
}

