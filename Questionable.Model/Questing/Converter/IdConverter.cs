using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Questionable.Model.Questing.Converter;

public class IdConverter : JsonConverter<IId>
{
    public override IId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        uint value = reader.GetUInt32();
        return Id.From(value);
    }

    public override void Write(Utf8JsonWriter writer, IId value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
