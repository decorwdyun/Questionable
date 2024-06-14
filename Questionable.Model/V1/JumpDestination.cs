using System.Numerics;
using System.Text.Json.Serialization;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

public sealed class JumpDestination
{
    [JsonConverter(typeof(VectorConverter))]
    public Vector3 Position { get; set; }

    public float? StopDistance { get; set; }
    public float? DelaySeconds { get; set; }
}
