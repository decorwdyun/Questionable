using System.Linq;
using Questionable.Model.V1;

namespace Questionable.Model;

internal sealed class Quest
{
    public required ushort QuestId { get; init; }
    public required string Name { get; set; }
    public required QuestData Data { get; init; }

    public QuestSequence? FindSequence(byte currentSequence)
        => Data.QuestSequence.SingleOrDefault(seq => seq.Sequence == currentSequence);
}
