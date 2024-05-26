using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Questionable.Model.V1.Converter;

public sealed class InteractionTypeConverter : JsonConverter<EInteractionType>
{
    private static readonly Dictionary<EInteractionType, string> EnumToString = new()
    {
        { EInteractionType.Interact, "Interact" },
        { EInteractionType.WalkTo, "WalkTo" },
        { EInteractionType.AttuneAethernetShard, "AttuneAethenetShard" },
        { EInteractionType.AttuneAetheryte, "AttuneAetheryte" },
        { EInteractionType.AttuneAetherCurrent, "AttuneAetherCurrent" },
        { EInteractionType.Combat, "Combat" },
        { EInteractionType.UseItem, "UseItem" },
        { EInteractionType.Emote, "Emote" },
        { EInteractionType.ManualAction, "ManualAction" }
    };

    private static readonly Dictionary<string, EInteractionType> StringToEnum =
        EnumToString.ToDictionary(x => x.Value, x => x.Key);

    public override EInteractionType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException();

        string? str = reader.GetString();
        if (str == null)
            throw new JsonException();

        return StringToEnum.TryGetValue(str, out EInteractionType value) ? value : throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, EInteractionType value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(EnumToString[value]);
    }
}
