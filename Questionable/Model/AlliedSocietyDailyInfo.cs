using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using LLib.GameData;
using Lumina.Excel.Sheets;
using Questionable.Data;
using Questionable.Model.Questing;

namespace Questionable.Model;

internal sealed class AlliedSocietyDailyInfo : IQuestInfo
{
    public AlliedSocietyDailyInfo(BeastTribe beastTribe, byte rank)
    {
        QuestId = new AlliedSocietyDailyId((byte)beastTribe.RowId, rank);
        Name = beastTribe.Name.ToString();
        ClassJobs = (EAlliedSociety)beastTribe.RowId switch
        {
            EAlliedSociety.Amaljaa or EAlliedSociety.Sylphs or EAlliedSociety.Kobolds or EAlliedSociety.Sahagin or
                EAlliedSociety.VanuVanu or EAlliedSociety.Vath or
                EAlliedSociety.Kojin or EAlliedSociety.Ananta or
                EAlliedSociety.Pixies or
                EAlliedSociety.Arkasodara or
                EAlliedSociety.Pelupelu =>
                [
                    ..ClassJobUtils.AsIndividualJobs(EExtendedClassJob.DoW),
                    ..ClassJobUtils.AsIndividualJobs(EExtendedClassJob.DoM)
                ],
            EAlliedSociety.Ixal or EAlliedSociety.Moogles or EAlliedSociety.Dwarves or EAlliedSociety.Loporrits =>
                ClassJobUtils.AsIndividualJobs(EExtendedClassJob.DoH).ToList(),

            EAlliedSociety.Qitari or EAlliedSociety.Omicrons =>
                ClassJobUtils.AsIndividualJobs(EExtendedClassJob.DoL).ToList(),

            EAlliedSociety.Namazu =>
            [
                ..ClassJobUtils.AsIndividualJobs(EExtendedClassJob.DoH),
                ..ClassJobUtils.AsIndividualJobs(EExtendedClassJob.DoL)
            ],

            _ => throw new ArgumentOutOfRangeException(nameof(beastTribe))
        };
        Expansion = (EExpansionVersion)beastTribe.Expansion.RowId;
    }

    public ElementId QuestId { get; }
    public string Name { get; }
    public uint IssuerDataId => 0;
    public ImmutableList<PreviousQuestInfo> PreviousQuests { get; } = [];
    public EQuestJoin PreviousQuestJoin => EQuestJoin.All;
    public bool IsRepeatable => true;
    public ushort Level => 1;
    public EAlliedSociety AlliedSociety => EAlliedSociety.None;
    public uint? JournalGenre => null;
    public ushort SortKey => 0;
    public bool IsMainScenarioQuest => false;
    public IReadOnlyList<EClassJob> ClassJobs { get; }
    public EExpansionVersion Expansion { get; }
}
