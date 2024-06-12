using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Questionable.Model.V1.Converter;

internal sealed class ExcelRefConverter : JsonConverter<ExcelRef>
{
    public override ExcelRef? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new ExcelRef(reader.GetString()!);
        else if (reader.TokenType == JsonTokenType.Number)
            return new ExcelRef(reader.GetUInt32());
        else
            return null;
    }

    public override void Write(Utf8JsonWriter writer, ExcelRef? value, JsonSerializerOptions options)
    {
        if (value == null)
            writer.WriteNullValue();
        else if (value.Type == ExcelRef.EType.Key)
            writer.WriteStringValue(value.AsKey());
        else if (value.Type == ExcelRef.EType.RowId)
            writer.WriteNumberValue(value.AsRowId());
        else
            throw new JsonException();
    }
}
