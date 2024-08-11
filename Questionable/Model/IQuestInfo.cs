using System;
using System.Collections.Generic;
using Dalamud.Game.Text;
using LLib.GameData;
using Questionable.Model.Questing;

namespace Questionable.Model;

public interface IQuestInfo
{
    public ElementId QuestId { get; }
    public string Name { get; }
    public uint IssuerDataId { get; }
    public bool IsRepeatable { get; }
    public ushort Level { get; }
    public EBeastTribe BeastTribe { get; }
    public uint? JournalGenre { get; }
    public ushort SortKey { get; }
    public bool IsMainScenarioQuest { get; }
    public IReadOnlyList<EClassJob> ClassJobs { get; }
    public EExpansionVersion Expansion { get; }

    public string SimplifiedName => Name
        .Replace(".", "", StringComparison.Ordinal)
        .TrimStart(SeIconChar.QuestSync.ToIconChar(), SeIconChar.QuestRepeatable.ToIconChar(), ' ');
}
