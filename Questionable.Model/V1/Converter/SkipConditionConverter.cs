using System.Collections.Generic;

namespace Questionable.Model.V1.Converter;

public sealed class SkipConditionConverter() : EnumConverter<ESkipCondition>(Values)
{
    private static readonly Dictionary<ESkipCondition, string> Values = new()
    {
        { ESkipCondition.Never, "Never" },
        { ESkipCondition.FlyingLocked, "FlyingLocked" },
        { ESkipCondition.FlyingUnlocked, "FlyingUnlocked" },
        { ESkipCondition.ChocoboUnlocked, "ChocoboUnlocked" },
        { ESkipCondition.AetheryteShortcutIfInSameTerritory, "AetheryteShortcutIfInSameTerritory" },
        { ESkipCondition.NotTargetable, "NotTargetable" },
        { ESkipCondition.ItemNotInInventory, "ItemNotInInventory" },
    };
}
