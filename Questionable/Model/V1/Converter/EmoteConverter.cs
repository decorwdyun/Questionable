using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Questionable.Model.V1.Converter;

public class EmoteConverter : JsonConverter<EEmote>
{
    private static readonly Dictionary<EEmote, string> EnumToString = new()
    {
        { EEmote.Stretch, "stretch" },
        { EEmote.Wave, "wave" },
        { EEmote.Rally, "rally" },
        { EEmote.Deny, "deny" },
    };

    private static readonly Dictionary<string, EEmote> StringToEnum =
        EnumToString.ToDictionary(x => x.Value, x => x.Key);

    public override EEmote Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException();

        string? str = reader.GetString();
        if (str == null)
            throw new JsonException();

        return StringToEnum.TryGetValue(str, out EEmote value) ? value : throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, EEmote value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(EnumToString[value]);
    }
}
