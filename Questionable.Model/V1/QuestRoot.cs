using System.Collections.Generic;

namespace Questionable.Model.V1;

public sealed class QuestRoot
{
    public string Author { get; set; } = null!;
    public List<string> Contributors { get; set; } = new();

    /// <summary>
    /// This is only relevant for release builds.
    /// </summary>
    public bool Disabled { get; set; }

    public string? Comment { get; set; }
    public List<ushort> TerritoryBlacklist { get; set; } = new();
    public List<QuestSequence> QuestSequence { get; set; } = new();
}
