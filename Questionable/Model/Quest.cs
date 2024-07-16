using System.Linq;
using Questionable.Model.V1;

namespace Questionable.Model;

internal sealed class Quest
{
    public required ushort QuestId { get; init; }
    public required QuestRoot Root { get; init; }
    public required QuestInfo Info { get; init; }
    public required bool ReadOnly { get; init; }

    public QuestSequence? FindSequence(byte currentSequence)
        => Root.QuestSequence.SingleOrDefault(seq => seq.Sequence == currentSequence);
}
