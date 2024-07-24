using System.Collections.Generic;

namespace Questionable.Model.V1.Converter;

public sealed class JumpTypeConverter() : EnumConverter<EJumpType>(Values)
{
    private static readonly Dictionary<EJumpType, string> Values = new()
    {
        { EJumpType.SingleJump, "SingleJump" },
        { EJumpType.RepeatedJumps, "RepeatedJumps" },
    };
}
