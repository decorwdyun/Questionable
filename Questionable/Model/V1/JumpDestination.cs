using System.Numerics;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
internal sealed class JumpDestination
{
    [JsonConverter(typeof(VectorConverter))]
    public Vector3 Position { get; set; }

    public float? StopDistance { get; set; }
    public float? DelaySeconds { get; set; }
}
