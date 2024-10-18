using System.Collections.Generic;
using System.Collections.Immutable;
using LLib.GameData;
using Lumina.Excel.GeneratedSheets2;
using Questionable.Model.Questing;

namespace Questionable.Model;

internal sealed class LeveInfo : IQuestInfo
{
    public LeveInfo(Leve leve)
    {
        QuestId = new LeveId((ushort)leve.RowId);
        Name = leve.Name;
        Level = leve.ClassJobLevel;
        JournalGenre = leve.JournalGenre.Row;
        SortKey = QuestId.Value;
        IssuerDataId = leve.LevelLevemete.Value!.Object.Row;
        ClassJobs = QuestInfoUtils.AsList(leve.ClassJobCategory.Value!);
        Expansion = (EExpansionVersion)leve.LevelLevemete.Value.Territory.Value!.ExVersion.Row;
    }

    public ElementId QuestId { get; }
    public string Name { get; }
    public uint IssuerDataId { get; }
    public ImmutableList<PreviousQuestInfo> PreviousQuests { get; } = [];
    public EQuestJoin PreviousQuestJoin => EQuestJoin.All;
    public bool IsRepeatable => true;
    public ushort Level { get; }
    public EAlliedSociety AlliedSociety => EAlliedSociety.None;
    public uint? JournalGenre { get; }
    public ushort SortKey { get; }
    public bool IsMainScenarioQuest => false;
    public IReadOnlyList<EClassJob> ClassJobs { get; }
    public EExpansionVersion Expansion { get; }
}
