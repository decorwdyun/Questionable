using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Dalamud.Game.Text;
using JetBrains.Annotations;
using ExcelQuest = Lumina.Excel.GeneratedSheets.Quest;

namespace Questionable.Model;

internal sealed class QuestInfo
{
    public QuestInfo(ExcelQuest quest)
    {
        QuestId = (ushort)(quest.RowId & 0xFFFF);
        Name = quest.Name.ToString();
        Level = quest.ClassJobLevel0;
        IssuerDataId = quest.IssuerStart;
        IsRepeatable = quest.IsRepeatable;
        PreviousQuests = quest.PreviousQuest.Select(x => (ushort)(x.Row & 0xFFFF)).Where(x => x != 0).ToImmutableList();
        PreviousQuestJoin = (QuestJoin)quest.PreviousQuestJoin;
        QuestLocks = quest.QuestLock.Select(x => (ushort)(x.Row & 0xFFFFF)).Where(x => x != 0).ToImmutableList();
        QuestLockJoin = (QuestJoin)quest.QuestLockJoin;
        IsMainScenarioQuest = quest.JournalGenre?.Value?.JournalCategory?.Value?.JournalSection?.Row is 0 or 1;
        CompletesInstantly = quest.ToDoCompleteSeq[0] == 0;
        PreviousInstanceContent = quest.InstanceContent.Select(x => (ushort)x.Row).Where(x => x != 0).ToList();
        PreviousInstanceContentJoin = (QuestJoin)quest.InstanceContentJoin;
    }


    public ushort QuestId { get; }
    public string Name { get; }
    public ushort Level { get; }
    public uint IssuerDataId { get; }
    public bool IsRepeatable { get; }
    public ImmutableList<ushort> PreviousQuests { get; }
    public QuestJoin PreviousQuestJoin { get; }
    public ImmutableList<ushort> QuestLocks { get; set; }
    public QuestJoin QuestLockJoin { get; set; }
    public List<ushort> PreviousInstanceContent { get; set; }
    public QuestJoin PreviousInstanceContentJoin { get; set; }
    public bool IsMainScenarioQuest { get; }
    public bool CompletesInstantly { get; set; }

    public string SimplifiedName => Name
        .TrimStart(SeIconChar.QuestSync.ToIconChar(), SeIconChar.QuestRepeatable.ToIconChar(), ' ');

    [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.Members)]
    public enum QuestJoin : byte
    {
        None = 0,
        All = 1,
        AtLeastOne = 2,
    }
}
