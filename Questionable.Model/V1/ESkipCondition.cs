using System.Text.Json.Serialization;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

[JsonConverter(typeof(SkipConditionConverter))]
public enum ESkipCondition
{
    None,
    Never,
    FlyingLocked,
    FlyingUnlocked,
    ChocoboUnlocked,
    AetheryteShortcutIfInSameTerritory,
    NotTargetable,
    ItemNotInInventory,

    // TODO: This is an indication the whole skip bit should be optimized/parameterized to some extent
    WakingSandsMainArea,
}
