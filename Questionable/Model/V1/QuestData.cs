using System.Collections.Generic;

namespace Questionable.Model.V1;

public class QuestData
{
    public required int Version { get; init; }
    public required List<QuestSequence> QuestSequence { get; set; }
}
