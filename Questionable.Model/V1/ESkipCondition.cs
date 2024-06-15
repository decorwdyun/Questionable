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
}
