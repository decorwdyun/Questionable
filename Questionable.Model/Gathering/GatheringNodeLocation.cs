using System.Numerics;

namespace Questionable.Model.Gathering;

public sealed class GatheringNodeLocation
{
    public uint DataId { get; set; }
    public Vector3 Position { get; set; }
    public float? MinimumAngle { get; set; }
    public float? MaximumAngle { get; set; }
    public float? MinimumDistance { get; set; } = 0.5f;
    public float? MaximumDistance { get; set; } = 3f;
}
