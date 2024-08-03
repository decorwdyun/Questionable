using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Questionable.Model.Questing.Converter;

public class ElementIdConverter : JsonConverter<ElementId>
{
    public override ElementId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        uint value = reader.GetUInt32();
        return ElementId.From(value);
    }

    public override void Write(Utf8JsonWriter writer, ElementId value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
