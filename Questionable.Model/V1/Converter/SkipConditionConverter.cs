using System.Collections.Generic;

namespace Questionable.Model.V1.Converter;

public sealed class SkipConditionConverter() : EnumConverter<EExtraSkipCondition>(Values)
{
    private static readonly Dictionary<EExtraSkipCondition, string> Values = new()
    {
        { EExtraSkipCondition.WakingSandsMainArea, "WakingSandsMainArea" },
    };
}
