using System.Collections.Generic;
using JetBrains.Annotations;

namespace Questionable.Model.V1;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
internal sealed class QuestData
{
    public required string Author { get; set; }
    public List<string> Contributors { get; set; } = new();
    public string? Comment { get; set; }
    public List<ushort> TerritoryBlacklist { get; set; } = new();
    public required List<QuestSequence> QuestSequence { get; set; } = new();
}
