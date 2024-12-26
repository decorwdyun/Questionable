using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using LLib.GameData;
using Questionable.Model.Questing;
using ExcelQuest = Lumina.Excel.Sheets.Quest;

namespace Questionable.Model;

internal sealed class QuestInfo : IQuestInfo
{
    public QuestInfo(ExcelQuest quest, uint newGamePlusChapter, byte startingCity)
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
        IssuerDataId = quest.IssuerStart.RowId;
        IsRepeatable = quest.IsRepeatable;
        PreviousQuests =
            new List<PreviousQuestInfo>
                {
                    new(ReplaceOldQuestIds((ushort)(quest.PreviousQuest[0].RowId & 0xFFFF)), quest.Unknown7),
                    new(ReplaceOldQuestIds((ushort)(quest.PreviousQuest[1].RowId & 0xFFFF))),
                    new(ReplaceOldQuestIds((ushort)(quest.PreviousQuest[2].RowId & 0xFFFF)))
                }
                .Where(x => x.QuestId.Value != 0)
                .ToImmutableList();
        PreviousQuestJoin = (EQuestJoin)quest.PreviousQuestJoin;
        QuestLocks = quest.QuestLock
            .Select(x => new QuestId((ushort)(x.RowId & 0xFFFFF)))
            .Where(x => x.Value != 0)
            .ToImmutableList();
        QuestLockJoin = (EQuestJoin)quest.QuestLockJoin;
        JournalGenre = quest.JournalGenre.ValueNullable?.RowId;
        SortKey = quest.SortKey;
        IsMainScenarioQuest = quest.JournalGenre.ValueNullable?.JournalCategory.ValueNullable?.JournalSection.ValueNullable?.RowId is 0 or 1;
        CompletesInstantly = quest.TodoParams[0].ToDoCompleteSeq == 0;
        PreviousInstanceContent = quest.InstanceContent.Select(x => (ushort)x.RowId).Where(x => x != 0).ToList();
        PreviousInstanceContentJoin = (EQuestJoin)quest.InstanceContentJoin;
        GrandCompany = (GrandCompany)quest.GrandCompany.RowId;
        AlliedSociety = (EAlliedSociety)quest.BeastTribe.RowId;
        AlliedSocietyQuestGroup = quest.Unknown11;
        AlliedSocietyRank = (int)quest.BeastReputationRank.RowId;
        ClassJobs = QuestInfoUtils.AsList(quest.ClassJobCategory0.ValueNullable!);
        IsSeasonalEvent = quest.Festival.RowId != 0;
        NewGamePlusChapter = newGamePlusChapter;
        StartingCity = startingCity;
        MoogleDeliveryLevel = (byte)quest.DeliveryQuest.RowId;
        Expansion = (EExpansionVersion)quest.Expansion.RowId;
    }

    private static QuestId ReplaceOldQuestIds(ushort questId)
    {
        return new QuestId(questId switch
        {
            524 => 4522,
            _ => questId,
        });
    }


    public ElementId QuestId { get; }
    public string Name { get; }
    public ushort Level { get; }
    public uint IssuerDataId { get; }
    public bool IsRepeatable { get; }
    public ImmutableList<PreviousQuestInfo> PreviousQuests { get; private set; }
    public EQuestJoin PreviousQuestJoin { get; }
    public ImmutableList<QuestId> QuestLocks { get; private set; }
    public EQuestJoin QuestLockJoin { get; private set; }
    public List<ushort> PreviousInstanceContent { get; }
    public EQuestJoin PreviousInstanceContentJoin { get; }
    public uint? JournalGenre { get; set; }
    public ushort SortKey { get; set; }
    public bool IsMainScenarioQuest { get; }
    public bool CompletesInstantly { get; }
    public GrandCompany GrandCompany { get; }
    public EAlliedSociety AlliedSociety { get; }
    public byte AlliedSocietyQuestGroup { get; }
    public int AlliedSocietyRank { get; }
    public IReadOnlyList<EClassJob> ClassJobs { get; }
    public bool IsSeasonalEvent { get; }
    public uint NewGamePlusChapter { get; }
    public byte StartingCity { get; set; }
    public byte MoogleDeliveryLevel { get; }
    public EExpansionVersion Expansion { get; }

    public void AddPreviousQuest(PreviousQuestInfo questId)
    {
        PreviousQuests = [..PreviousQuests, questId];
    }

    public void AddQuestLocks(EQuestJoin questJoin, params QuestId[] questId)
    {
        if (QuestLocks.Count > 0 && QuestLockJoin != questJoin)
            throw new InvalidOperationException();

        QuestLockJoin = questJoin;
        QuestLocks = [..QuestLocks, ..questId];
    }
}
