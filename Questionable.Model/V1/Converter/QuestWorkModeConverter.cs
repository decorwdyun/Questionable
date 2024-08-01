using System.Collections.Generic;

namespace Questionable.Model.V1.Converter;

public sealed class QuestWorkModeConverter() : EnumConverter<EQuestWorkMode>(Values)
{
    private static readonly Dictionary<EQuestWorkMode, string> Values = new()
    {
        { EQuestWorkMode.Bitwise, "Bitwise" },
        { EQuestWorkMode.Exact, "Exact" },
    };
}
