using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Questionable.Model.V1.Converter;

public class EnemySpawnTypeConverter : JsonConverter<EEnemySpawnType>
{
    private static readonly Dictionary<EEnemySpawnType, string> EnumToString = new()
    {
        { EEnemySpawnType.AfterInteraction, "AfterInteraction" },
        { EEnemySpawnType.AutoOnEnterArea, "AutoOnEnterArea" },
    };

    private static readonly Dictionary<string, EEnemySpawnType> StringToEnum =
        EnumToString.ToDictionary(x => x.Value, x => x.Key);

    public override EEnemySpawnType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException();

        string? str = reader.GetString();
        if (str == null)
            throw new JsonException();

        return StringToEnum.TryGetValue(str, out EEnemySpawnType value) ? value : throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, EEnemySpawnType value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(EnumToString[value]);
    }

}
