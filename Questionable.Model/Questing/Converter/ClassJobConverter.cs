using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Questionable.Model.Questing.Converter;

internal sealed class ClassJobConverter : JsonConverter<uint>
{
    public override uint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException();

        return reader.GetString() switch
        {
            "Miner" => 16,
            "Botanist" => 17,
            _ => throw new JsonException("Unsupported value for classjob"),
        };
    }

    public override void Write(Utf8JsonWriter writer, uint value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
