using System.Collections.Generic;
using System.Linq;

namespace Questionable.Model.Questing;

public sealed class SkipStepConditions
{
    public bool Never { get; set; }
    public IList<QuestWorkValue?> CompletionQuestVariablesFlags { get; set; } = new List<QuestWorkValue?>();
    public ELockedSkipCondition? Flying { get; set; }
    public ELockedSkipCondition? Chocobo { get; set; }
    public bool NotTargetable { get; set; }
    public List<ushort> InTerritory { get; set; } = new();
    public List<ushort> NotInTerritory { get; set; } = new();
    public SkipItemConditions? Item { get; set; }
    public List<IId> QuestsAccepted { get; set; } = new();
    public List<IId> QuestsCompleted { get; set; } = new();
    public EExtraSkipCondition? ExtraCondition { get; set; }

    public bool HasSkipConditions()
    {
        if (Never)
            return false;
        return (CompletionQuestVariablesFlags.Count > 0 && CompletionQuestVariablesFlags.Any(x => x != null)) ||
               Flying != null ||
               Chocobo != null ||
               NotTargetable ||
               InTerritory.Count > 0 ||
               NotInTerritory.Count > 0 ||
               Item != null ||
               QuestsAccepted.Count > 0 ||
               QuestsCompleted.Count > 0 ||
               ExtraCondition != null;
    }

    public override string ToString()
    {
        return
            $"{nameof(Never)}: {Never}, {nameof(CompletionQuestVariablesFlags)}: {CompletionQuestVariablesFlags}, {nameof(Flying)}: {Flying}, {nameof(Chocobo)}: {Chocobo}, {nameof(NotTargetable)}: {NotTargetable}, {nameof(InTerritory)}: {string.Join(" ", InTerritory)}, {nameof(NotInTerritory)}: {string.Join(" ", NotInTerritory)}, {nameof(Item)}: {Item}, {nameof(QuestsAccepted)}: {string.Join(" ", QuestsAccepted)}, {nameof(QuestsCompleted)}: {string.Join(" ", QuestsCompleted)}, {nameof(ExtraCondition)}: {ExtraCondition}";
    }
}
