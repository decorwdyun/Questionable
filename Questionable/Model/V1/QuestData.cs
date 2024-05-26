using System.Collections.Generic;

namespace Questionable.Model.V1;

public class QuestData
{
    public required int Version { get; set; }
    public required string Author { get; set; }
    public List<string> Contributors { get; set; } = new();
    public string Comment { get; set; }
    public List<ushort> TerritoryBlacklist { get; set; } = new();
    public required List<QuestSequence> QuestSequence { get; set; } = new();
}
