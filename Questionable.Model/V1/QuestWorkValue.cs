using System.Text.Json.Serialization;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

[JsonConverter(typeof(QuestWorkConfigConverter))]
public sealed class QuestWorkValue(byte? high, byte? low)
{
    public QuestWorkValue(byte value)
        : this((byte)(value >> 4), (byte)(value & 0xF))
    {
    }

    public byte? High { get; set; } = high;
    public byte? Low { get; set; } = low;
}
