using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using JetBrains.Annotations;
using LLib.GameData;
using Questionable.Model.Questing;
using ExcelQuest = Lumina.Excel.GeneratedSheets2.Quest;

namespace Questionable.Model;

internal sealed class QuestInfo : IQuestInfo
{
    public QuestInfo(ExcelQuest quest, ushort newGamePlusChapter, byte startingCity)
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
            1432 => " (Gridania)",
            1433 => " (Limsa)",
            1434 => " (Ul'dah)",
            _ => "",
        };

        Name = $"{quest.Name}{suffix}";
        Level = quest.ClassJobLevel[0];
        IssuerDataId = quest.IssuerStart.Row;
        IsRepeatable = quest.IsRepeatable;
        PreviousQuests =
            new List<PreviousQuestInfo>
                {
                    new(new QuestId((ushort)(quest.PreviousQuest[0].Row & 0xFFFF)), quest.Unknown7),
                    new(new QuestId((ushort)(quest.PreviousQuest[1].Row & 0xFFFF))),
                    new(new QuestId((ushort)(quest.PreviousQuest[2].Row & 0xFFFF)))
                }
                .Where(x => x.QuestId.Value != 0)
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
        CompletesInstantly = quest.TodoParams[0].ToDoCompleteSeq == 0;
        PreviousInstanceContent = quest.InstanceContent.Select(x => (ushort)x.Row).Where(x => x != 0).ToList();
        PreviousInstanceContentJoin = (QuestJoin)quest.InstanceContentJoin;
        GrandCompany = (GrandCompany)quest.GrandCompany.Row;
        AlliedSociety = (EAlliedSociety)quest.BeastTribe.Row;
        ClassJobs = QuestInfoUtils.AsList(quest.ClassJobCategory0.Value!);
        IsSeasonalEvent = quest.Festival.Row != 0;
        NewGamePlusChapter = newGamePlusChapter;
        StartingCity = startingCity;
        Expansion = (EExpansionVersion)quest.Expansion.Row;
    }


    public ElementId QuestId { get; }
    public string Name { get; }
    public ushort Level { get; }
    public uint IssuerDataId { get; }
    public bool IsRepeatable { get; }
    public ImmutableList<PreviousQuestInfo> PreviousQuests { get; private set; }
    public QuestJoin PreviousQuestJoin { get; }
    public ImmutableList<QuestId> QuestLocks { get; private set; }
    public QuestJoin QuestLockJoin { get; private set; }
    public List<ushort> PreviousInstanceContent { get; }
    public QuestJoin PreviousInstanceContentJoin { get; }
    public uint? JournalGenre { get; }
    public ushort SortKey { get; }
    public bool IsMainScenarioQuest { get; }
    public bool CompletesInstantly { get; }
    public GrandCompany GrandCompany { get; }
    public EAlliedSociety AlliedSociety { get; }
    public IReadOnlyList<EClassJob> ClassJobs { get; }
    public bool IsSeasonalEvent { get; }
    public ushort NewGamePlusChapter { get; }
    public byte StartingCity { get; set; }
    public EExpansionVersion Expansion { get; }

    [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.Members)]
    public enum QuestJoin : byte
    {
        None = 0,
        All = 1,
        AtLeastOne = 2,
    }

    public void AddPreviousQuest(PreviousQuestInfo questId)
    {
        PreviousQuests = [..PreviousQuests, questId];
    }

    public void AddQuestLocks(QuestJoin questJoin, params QuestId[] questId)
    {
        if (QuestLocks.Count > 0 && QuestLockJoin != questJoin)
            throw new InvalidOperationException();

        QuestLockJoin = questJoin;
        QuestLocks = [..QuestLocks, ..questId];
    }

    public sealed record PreviousQuestInfo(QuestId QuestId, byte Sequence = 0);
}
