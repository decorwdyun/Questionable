using System.Numerics;
using System.Text.Json.Serialization;
using Questionable.Model.Common.Converter;

namespace Questionable.Model.Gathering;

public sealed class GatheringLocation
{
    [JsonConverter(typeof(VectorConverter))]
    public Vector3 Position { get; set; }

    public float? MinimumAngle { get; set; }
    public float? MaximumAngle { get; set; }
    public float MinimumDistance { get; set; } = 1f;
    public float MaximumDistance { get; set; } = 3f;

    public bool IsCone()
    {
        return MinimumAngle != null && MaximumAngle != null;
    }
}
