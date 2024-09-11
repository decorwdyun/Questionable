﻿using System.Collections.Generic;
using System.Collections.Immutable;
using LLib.GameData;
using Lumina.Excel.GeneratedSheets;
using Questionable.Model.Questing;

namespace Questionable.Model;

internal sealed class SatisfactionSupplyInfo : IQuestInfo
{
    public SatisfactionSupplyInfo(SatisfactionNpc npc)
    {
        QuestId = new SatisfactionSupplyNpcId((ushort)npc.RowId);
        Name = npc.Npc.Value!.Singular;
        IssuerDataId = npc.Npc.Row;
        Level = npc.LevelUnlock;
        SortKey = QuestId.Value;
        Expansion = (EExpansionVersion)npc.QuestRequired.Value!.Expansion.Row;
        PreviousQuests = [new PreviousQuestInfo(new QuestId((ushort)(npc.QuestRequired.Row & 0xFFFF)))];
    }

    public ElementId QuestId { get; }
    public string Name { get; }
    public uint IssuerDataId { get; }
    public bool IsRepeatable => true;
    public ImmutableList<PreviousQuestInfo> PreviousQuests { get; }
    public EQuestJoin PreviousQuestJoin => EQuestJoin.All;
    public ushort Level { get; }
    public EAlliedSociety AlliedSociety => EAlliedSociety.None;
    public uint? JournalGenre => null;
    public ushort SortKey { get; }
    public bool IsMainScenarioQuest => false;
    public EExpansionVersion Expansion { get; }

    /// <summary>
    /// We don't have collectables implemented for any other class.
    /// </summary>
    public IReadOnlyList<EClassJob> ClassJobs { get; } = [EClassJob.Miner, EClassJob.Botanist];
}
