using System.Numerics;

namespace Questionable.Model.V1;

public class QuestStep
{
    public required string InteractionType { get; set; }
    public ulong? DataId { get; set; }
    public Vector3 Position { get; set; }
    public ushort TerritoryId { get; set; }
}
