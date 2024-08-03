using System.Collections.Generic;

namespace Questionable.Model.Questing;

public sealed class SkipAetheryteCondition
{
    public bool Never { get; set; }
    public bool InSameTerritory { get; set; }
    public List<ushort> InTerritory { get; set; } = new();
}
