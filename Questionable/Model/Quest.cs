using Questionable.Model.V1;

namespace Questionable;

internal sealed class Quest
{
    public required string FilePath { get; init; }

    public required ushort QuestId { get; init; }
    public required string Name { get; init; }
    public required QuestData Data { get; init; }
}
