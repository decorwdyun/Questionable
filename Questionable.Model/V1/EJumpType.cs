using System.Text.Json.Serialization;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

[JsonConverter(typeof(JumpTypeConverter))]
public enum EJumpType
{
    SingleJump,
    RepeatedJumps,
}
