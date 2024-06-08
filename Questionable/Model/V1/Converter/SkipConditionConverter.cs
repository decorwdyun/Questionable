using System.Collections.Generic;

namespace Questionable.Model.V1.Converter;

internal sealed class SkipConditionConverter() : EnumConverter<ESkipCondition>(Values)
{
    private static readonly Dictionary<ESkipCondition, string> Values = new()
    {
        { ESkipCondition.Never, "Never" },
        { ESkipCondition.FlyingLocked, "FlyingLocked" },
        { ESkipCondition.FlyingUnlocked, "FlyingUnlocked" },
    };
}
