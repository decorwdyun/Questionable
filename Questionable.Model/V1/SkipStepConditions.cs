using System.Collections.Generic;

namespace Questionable.Model.V1;

public sealed class SkipStepConditions
{
    public bool Never { get; set; }
    public ELockedSkipCondition? Flying { get; set; }
    public ELockedSkipCondition? Chocobo { get; set; }
    public bool NotTargetable { get; set; }
    public List<ushort> InTerritory { get; set; } = new();
    public List<ushort> NotInTerritory { get; set; } = new();
    public SkipItemConditions? Item { get; set; }

    // TODO not implemented
    public List<ushort> QuestsAccepted { get; set; } = new();

    // TODO not implemented
    public List<ushort> QuestsCompleted { get; set; } = new();
    public EExtraSkipCondition? ExtraCondition { get; set; }

    public bool HasSkipConditions()
    {
        return Never || Flying != null || Chocobo != null || InTerritory.Count > 0 || NotInTerritory.Count > 0 || Item != null;
    }
}
