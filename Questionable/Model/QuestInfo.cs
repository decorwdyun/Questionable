using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Dalamud.Game.Text;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using JetBrains.Annotations;
using Questionable.Model.Questing;
using ExcelQuest = Lumina.Excel.GeneratedSheets.Quest;

namespace Questionable.Model;

internal sealed class QuestInfo
{
    public QuestInfo(ExcelQuest quest)
    {
        QuestId = new QuestId((ushort)(quest.RowId & 0xFFFF));

        string suffix = QuestId.Value switch
        {
            85 => " (Lancer)",
            108 => " (Marauder)",
            109 => " (Arcanist)",
            123 => " (Archer)",
            124 => " (Conjurer)",
            568 => " (Gladiator)",
            569 => " (Pugilist)",
            570 => " (Thaumaturge)",
            673 => " (Ul'dah)",
            674 => " (Limsa/Gridania)",
            _ => "",
        };

        Name = $"{quest.Name}{suffix}";
        Level = quest.ClassJobLevel0;
        IssuerDataId = quest.IssuerStart;
        IsRepeatable = quest.IsRepeatable;
        PreviousQuests = quest.PreviousQuest
            .Select(x => new QuestId((ushort)(x.Row & 0xFFFF)))
            .Where(x => x.Value != 0)
            .ToImmutableList();
        PreviousQuestJoin = (QuestJoin)quest.PreviousQuestJoin;
        QuestLocks = quest.QuestLock
            .Select(x => new QuestId((ushort)(x.Row & 0xFFFFF)))
            .Where(x => x.Value != 0)
            .ToImmutableList();
        QuestLockJoin = (QuestJoin)quest.QuestLockJoin;
        JournalGenre = quest.JournalGenre?.Row;
        SortKey = quest.SortKey;
        IsMainScenarioQuest = quest.JournalGenre?.Value?.JournalCategory?.Value?.JournalSection?.Row is 0 or 1;
        CompletesInstantly = quest.ToDoCompleteSeq[0] == 0;
        PreviousInstanceContent = quest.InstanceContent.Select(x => (ushort)x.Row).Where(x => x != 0).ToList();
        PreviousInstanceContentJoin = (QuestJoin)quest.InstanceContentJoin;
        GrandCompany = (GrandCompany)quest.GrandCompany.Row;
        BeastTribe = (EBeastTribe)quest.BeastTribe.Row;
    }


    public QuestId QuestId { get; }
    public string Name { get; }
    public ushort Level { get; }
    public uint IssuerDataId { get; }
    public bool IsRepeatable { get; }
    public ImmutableList<QuestId> PreviousQuests { get; }
    public QuestJoin PreviousQuestJoin { get; }
    public ImmutableList<QuestId> QuestLocks { get; }
    public QuestJoin QuestLockJoin { get; }
    public List<ushort> PreviousInstanceContent { get; }
    public QuestJoin PreviousInstanceContentJoin { get; }
    public uint? JournalGenre { get; }
    public ushort SortKey { get; set; }
    public bool IsMainScenarioQuest { get; }
    public bool CompletesInstantly { get; }
    public GrandCompany GrandCompany { get; }
    public EBeastTribe BeastTribe { get; }

    public string SimplifiedName => Name
        .Replace(".", "", StringComparison.Ordinal)
        .TrimStart(SeIconChar.QuestSync.ToIconChar(), SeIconChar.QuestRepeatable.ToIconChar(), ' ');

    [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.Members)]
    public enum QuestJoin : byte
    {
        None = 0,
        All = 1,
        AtLeastOne = 2,
    }
}
