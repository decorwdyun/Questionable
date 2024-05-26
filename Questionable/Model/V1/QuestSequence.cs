using System.Collections.Generic;

namespace Questionable.Model.V1;

public class QuestSequence
{
    public required int Sequence { get; set; }
    public string Comment { get; set; }
    public List<QuestStep> Steps { get; set; } = new();
}
