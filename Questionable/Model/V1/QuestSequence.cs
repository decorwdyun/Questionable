using System.Collections.Generic;

namespace Questionable.Model.V1;

public class QuestSequence
{
    public int Sequence { get; set; }
    public List<QuestStep> Steps { get; set; } = new();
}
