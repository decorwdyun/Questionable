using System.Collections.Generic;

namespace Questionable.Model.V1;

public sealed class ComplexCombatData
{
    public uint DataId { get; set; }

    // TODO Use this
    public uint? MinimumKillCount { get; set; }

    public uint? RewardItemId { get; set; }
    public int? RewardItemCount { get; set; }
    public IList<short?> CompletionQuestVariablesFlags { get; set; } = new List<short?>();
}
