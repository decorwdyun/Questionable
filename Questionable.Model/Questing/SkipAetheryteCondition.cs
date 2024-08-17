using System.Collections.Generic;
using Questionable.Model.Common;

namespace Questionable.Model.Questing;

public sealed class SkipAetheryteCondition
{
    public bool Never { get; set; }
    public bool InSameTerritory { get; set; }
    public List<ushort> InTerritory { get; set; } = new();
    public EAetheryteLocation? AetheryteLocked { get; set; }
    public EAetheryteLocation? AetheryteUnlocked { get; set; }
    public bool RequiredQuestVariablesNotMet { get; set; }
    public NearPositionCondition? NearPosition { get; set; }
}
