using System.Collections.Generic;
using JetBrains.Annotations;

namespace Questionable.Model.V1;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
internal sealed class QuestSequence
{
    public required int Sequence { get; set; }
    public string? Comment { get; set; }
    public List<QuestStep> Steps { get; set; } = new();

    public QuestStep? FindStep(int step)
    {
        if (step < 0 || step >= Steps.Count)
            return null;

        return Steps[step];
    }
}
