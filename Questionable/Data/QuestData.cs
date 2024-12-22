using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using LLib.GameData;
using Lumina.Excel.Sheets;
using Questionable.Model;
using Questionable.Model.Questing;
using Quest = Lumina.Excel.Sheets.Quest;

namespace Questionable.Data;

internal sealed class QuestData
{
    public static readonly IReadOnlyList<QuestId> CrystalTowerQuests =
        [new(1709), new(1200), new(1201), new(1202), new(1203), new(1474), new(494), new(495)];

    public static readonly IReadOnlyList<uint> TankRoleQuests = [136, 154, 178];
    public static readonly IReadOnlyList<uint> HealerRoleQuests = [137, 155, 179];
    public static readonly IReadOnlyList<uint> MeleeRoleQuests = [138, 156, 180];
    public static readonly IReadOnlyList<uint> PhysicalRangedRoleQuests = [138, 157, 181];
    public static readonly IReadOnlyList<uint> CasterRoleQuests = [139, 158, 182];

    public static readonly IReadOnlyList<IReadOnlyList<uint>> AllRoleQuestChapters =
    [
        TankRoleQuests,
        HealerRoleQuests,
        MeleeRoleQuests,
        PhysicalRangedRoleQuests,
        CasterRoleQuests
    ];

    public static readonly IReadOnlyList<QuestId> FinalShadowbringersRoleQuests =
        [new(3248), new(3272), new(3278), new(3628)];

    private readonly Dictionary<ElementId, IQuestInfo> _quests;

    public QuestData(IDataManager dataManager)
    {
        Dictionary<uint, uint> questChapters =
            dataManager.GetExcelSheet<QuestChapter>()
                .Where(x => x.RowId > 0 && x.Quest.RowId > 0)
                .ToDictionary(x => x.Quest.RowId, x => x.Redo.RowId);

        Dictionary<uint, byte> startingCities = new();
        for (byte redoChapter = 1; redoChapter <= 3; ++redoChapter)
        {
            var questRedo = dataManager.GetExcelSheet<QuestRedo>().GetRow(redoChapter);
            foreach (var quest in questRedo.QuestRedoParam.Where(x => x.Quest.IsValid))
                startingCities[quest.Quest.RowId] = redoChapter;
        }

        List<IQuestInfo> quests =
        [
            ..dataManager.GetExcelSheet<Quest>()
                .Where(x => x.RowId > 0)
                .Where(x => x.IssuerLocation.RowId > 0)
                .Select(x => new QuestInfo(x, questChapters.GetValueOrDefault(x.RowId),
                    startingCities.GetValueOrDefault(x.RowId))),
            ..dataManager.GetExcelSheet<SatisfactionNpc>()
                .Where(x => x is { RowId: > 0, Npc.RowId: > 0 })
                .Select(x => new SatisfactionSupplyInfo(x)),
            ..dataManager.GetExcelSheet<Leve>()
                .Where(x => x.RowId > 0)
                .Where(x => x.LevelLevemete.RowId != 0)
                .Select(x => new LeveInfo(x)),
        ];

        quests.AddRange(
            dataManager.GetExcelSheet<BeastTribe>()
                .Where(x => x.RowId > 0 && !x.Name.IsEmpty)
                .SelectMany(x =>
                {
                    if (x.RowId < 5)
                    {
                        return ((IEnumerable<byte>)
                            [
                                0,
                                ..quests.Where(y => y.AlliedSociety == (EAlliedSociety)x.RowId && y.IsRepeatable)
                                    .Cast<QuestInfo>()
                                    .Select(y => (byte)y.AlliedSocietyRank).Distinct()
                            ])
                            .Select(rank => new AlliedSocietyDailyInfo(x, rank));
                    }
                    else
                    {
                        return [new AlliedSocietyDailyInfo(x, 0)];
                    }
                }));

        _quests = quests.ToDictionary(x => x.QuestId, x => x);

        // workaround because the game doesn't require completion of the CT questline through normal means
        AddPreviousQuest(new QuestId(425), new QuestId(495));

        // "In order to undertake this quest" [...]
        const int mountaintopDiplomacy = 1619;
        const int inscrutableTastes = 2095;
        const int tideGoesIn = 2490;
        const int firstOfMany = 2534;
        const int achtIaOrmhInn = 3320;
        AddPreviousQuest(new QuestId(1480), new QuestId(2373));
        AddPreviousQuest(new QuestId(1717), new QuestId(mountaintopDiplomacy));
        AddPreviousQuest(new QuestId(2088), new QuestId(mountaintopDiplomacy));
        AddPreviousQuest(new QuestId(2062), new QuestId(1617));
        AddPreviousQuest(new QuestId(2063), new QuestId(mountaintopDiplomacy));
        AddPreviousQuest(new QuestId(2257), new QuestId(1655));
        AddPreviousQuest(new QuestId(2608), new QuestId(firstOfMany));
        AddPreviousQuest(new QuestId(2600), new QuestId(2466));
        AddPreviousQuest(new QuestId(2622), new QuestId(tideGoesIn));
        AddPreviousQuest(new QuestId(2624), new QuestId(firstOfMany));
        AddPreviousQuest(new QuestId(2898), new QuestId(tideGoesIn));
        AddPreviousQuest(new QuestId(2974), new QuestId(2491));
        AddPreviousQuest(new QuestId(2975), new QuestId(2630));
        AddPreviousQuest(new QuestId(2912), new QuestId(tideGoesIn));
        AddPreviousQuest(new QuestId(2914), new QuestId(2537));
        AddPreviousQuest(new QuestId(2919), new QuestId(2455));
        AddPreviousQuest(new QuestId(2952), new QuestId(2518));
        AddPreviousQuest(new QuestId(2904), new QuestId(2503));
        AddPreviousQuest(new QuestId(3038), new QuestId(firstOfMany));
        AddPreviousQuest(new QuestId(3087), new QuestId(100));
        AddPreviousQuest(new QuestId(3246), new QuestId(3314));
        AddPreviousQuest(new QuestId(3247), new QuestId(achtIaOrmhInn));
        AddPreviousQuest(new QuestId(3270), new QuestId(3333));
        AddPreviousQuest(new QuestId(3271), new QuestId(3634));
        AddPreviousQuest(new QuestId(3264), new QuestId(2247));
        AddPreviousQuest(new QuestId(3253), new QuestId(2247));
        AddPreviousQuest(new QuestId(3254), new QuestId(2537));
        AddPreviousQuest(new QuestId(3228), new QuestId(achtIaOrmhInn));
        AddPreviousQuest(new QuestId(3234), new QuestId(achtIaOrmhInn));
        AddPreviousQuest(new QuestId(3237), new QuestId(achtIaOrmhInn));
        AddPreviousQuest(new QuestId(3238), new QuestId(3634));
        AddPreviousQuest(new QuestId(3240), new QuestId(achtIaOrmhInn));
        AddPreviousQuest(new QuestId(3241), new QuestId(3648));
        AddPreviousQuest(new QuestId(3628), new QuestId(3301));
        AddPreviousQuest(new QuestId(3655), new QuestId(inscrutableTastes));
        AddPreviousQuest(new QuestId(3771), new QuestId(495));
        AddPreviousQuest(new QuestId(4068), new QuestId(1658));
        AddPreviousQuest(new QuestId(4078), new QuestId(1583));
        AddPreviousQuest(new QuestId(4150), new QuestId(4417));
        AddPreviousQuest(new QuestId(4155), new QuestId(4383));
        AddPreviousQuest(new QuestId(4156), new QuestId(3326));
        AddPreviousQuest(new QuestId(4158), new QuestId(4434));
        AddPreviousQuest(new QuestId(4159), new QuestId(4464));
        AddPreviousQuest(new QuestId(4163), new QuestId(4398));
        AddPreviousQuest(new QuestId(4165), new QuestId(4438));
        AddPreviousQuest(new QuestId(4473), new QuestId(inscrutableTastes));
        AddPreviousQuest(new QuestId(4650), new QuestId(2374));
        AddPreviousQuest(new QuestId(4662), new QuestId(3166));
        AddPreviousQuest(new QuestId(4761), new QuestId(4032));
        AddPreviousQuest(new QuestId(4812), new QuestId(4750));
        AddPreviousQuest(new QuestId(4851), new QuestId(2446));
        AddPreviousQuest(new QuestId(4856), new QuestId(1669));
        AddPreviousQuest(new QuestId(4857), new QuestId(2553));
        AddPreviousQuest(new QuestId(4979), new QuestId(4896));
        AddPreviousQuest(new QuestId(4980), new QuestId(4911));
        AddPreviousQuest(new QuestId(4985), new QuestId(4903));
        AddPreviousQuest(new QuestId(4987), new QuestId(4912));
        AddPreviousQuest(new QuestId(4988), new QuestId(4942));
        AddPreviousQuest(new QuestId(4992), new QuestId(4912));
        AddPreviousQuest(new QuestId(4999), new QuestId(4908));
        AddPreviousQuest(new QuestId(4966), new QuestId(inscrutableTastes));
        AddPreviousQuest(new QuestId(5000), new QuestId(4908));
        AddPreviousQuest(new QuestId(5001), new QuestId(4912));

        // "In order to proceed with this quest" [...]
        /* my little chocobo
        AddPreviousQuest(new QuestId(1036), new QuestId());
        AddPreviousQuest(new QuestId(1663), new QuestId());
        AddPreviousQuest(new QuestId(3771), new QuestId());
        AddPreviousQuest(new QuestId(4521), new QuestId());
        */
        /* only applicable for fishers
        const int spearfishing = 2922;
        AddPreviousQuest(new QuestId(3811), new QuestId(spearfishing));
        AddPreviousQuest(new QuestId(3812), new QuestId(spearfishing));
        AddPreviousQuest(new QuestId(3817), new QuestId(spearfishing));
        AddPreviousQuest(new QuestId(3818), new QuestId(spearfishing));
        AddPreviousQuest(new QuestId(3821), new QuestId(spearfishing));
        AddPreviousQuest(new QuestId(3833), new QuestId(spearfishing));
        */

        // The Hero's Journey
        AddPreviousQuest(new QuestId(3986), new QuestId(2115));
        AddPreviousQuest(new QuestId(3986), new QuestId(2116));
        AddPreviousQuest(new QuestId(3986), new QuestId(2281));
        AddPreviousQuest(new QuestId(3986), new QuestId(2333));
        AddPreviousQuest(new QuestId(3986), new QuestId(2395));
        AddPreviousQuest(new QuestId(3986), new QuestId(3985));

        // initial city quests are side quests
        // unclear if 470 can be started as the required quest isn't available anymore
        ushort[] limsaSideQuests =
            [107, 111, 112, 122, 663, 475, 472, 476, 470, 473, 474, 477, 486, 478, 479, 59, 400, 401, 693, 405];
        foreach (var questId in limsaSideQuests)
            ((QuestInfo)_quests[new QuestId(questId)]).StartingCity = 1;

        ushort[] gridaniaQuests =
            [39, 1, 32, 34, 37, 172, 127, 130, 60, 220, 378];
        foreach (var questId in gridaniaQuests)
            ((QuestInfo)_quests[new QuestId(questId)]).StartingCity = 2;

        ushort[] uldahSideQuests =
            [594, 389, 390, 321, 304, 322, 388, 308, 326, 58, 687, 341, 504, 531, 506, 530, 573, 342, 505];
        foreach (var questId in uldahSideQuests)
            ((QuestInfo)_quests[new QuestId(questId)]).StartingCity = 3;

        // follow-up quests to picking a GC
        AddGcFollowUpQuests();

        // update relic quests to be in a different journal category
        ushort[] zodiacStartingQuests = [1119, 1120, 1121, 1122, 1123, 1124, 1125, 1126, 1127, 1579];
        foreach (var questId in zodiacStartingQuests)
        {
            var quest = ((QuestInfo)_quests[new QuestId(questId)]);
            quest.JournalGenre = 82;
            quest.SortKey = 0;
        }
    }

    private void AddPreviousQuest(QuestId questToUpdate, QuestId requiredQuestId)
    {
        QuestInfo quest = (QuestInfo)_quests[questToUpdate];
        quest.AddPreviousQuest(new PreviousQuestInfo(requiredQuestId));
    }

    private void AddGcFollowUpQuests()
    {
        QuestId[] questIds = [new(683), new(684), new(685)];
        foreach (QuestId questId in questIds)
        {
            QuestInfo quest = (QuestInfo)_quests[questId];
            quest.AddQuestLocks(EQuestJoin.AtLeastOne, questIds.Where(x => x != questId).ToArray());
        }
    }

    public IQuestInfo GetQuestInfo(ElementId elementId)
    {
        return _quests[elementId] ?? throw new ArgumentOutOfRangeException(nameof(elementId));
    }

    public bool TryGetQuestInfo(ElementId elementId, [NotNullWhen(true)] out IQuestInfo? questInfo)
    {
        return _quests.TryGetValue(elementId, out questInfo);
    }

    public List<IQuestInfo> GetAllByIssuerDataId(uint targetId)
    {
        return _quests.Values
            .Where(x => x.IssuerDataId == targetId)
            .ToList();
    }

    public bool IsIssuerOfAnyQuest(uint targetId) => _quests.Values.Any(x => x.IssuerDataId == targetId);

    public List<IQuestInfo> GetAllByJournalGenre(uint journalGenre)
    {
        return _quests.Values
            .Where(x => x is QuestInfo { IsSeasonalEvent: false } or not QuestInfo)
            .Where(x => x.JournalGenre == journalGenre)
            .OrderBy(x => x.SortKey)
            .ThenBy(x => x.QuestId)
            .ToList();
    }

    public List<QuestInfo> GetAllByAlliedSociety(EAlliedSociety alliedSociety)
    {
        return _quests.Values
            .Where(x => x is QuestInfo)
            .Cast<QuestInfo>()
            .Where(x => x.AlliedSociety == alliedSociety)
            .OrderBy(x => x.QuestId)
            .ToList();
    }

    public List<QuestInfo> GetClassJobQuests(EClassJob classJob)
    {
        List<uint> chapterIds = classJob switch
        {
            EClassJob.Adventurer => throw new ArgumentOutOfRangeException(nameof(classJob)),

            // ARR
            EClassJob.Gladiator => [63],
            EClassJob.Paladin => [72, 73, 74],
            EClassJob.Marauder => [64],
            EClassJob.Warrior => [76, 77, 78],
            EClassJob.Conjurer => [65],
            EClassJob.WhiteMage => [86, 87, 88],
            EClassJob.Arcanist => [66],
            EClassJob.Summoner => [127, 128, 129],
            EClassJob.Scholar => [90, 91, 92],
            EClassJob.Pugilist => [67],
            EClassJob.Monk => [98, 99, 100],
            EClassJob.Lancer => [68],
            EClassJob.Dragoon => [102, 103, 104],
            EClassJob.Rogue => [69],
            EClassJob.Ninja => [106, 107, 108],
            EClassJob.Archer => [70],
            EClassJob.Bard => [113, 114, 115],
            EClassJob.Thaumaturge => [71],
            EClassJob.BlackMage => [123, 124, 125],

            // HW
            EClassJob.DarkKnight => [80, 81, 82],
            EClassJob.Astrologian => [94, 95, 96],
            EClassJob.Machinist => [117, 118, 119],

            // SB
            EClassJob.Samurai => [110, 111],
            EClassJob.RedMage => [131, 132],
            EClassJob.BlueMage => [134, 135, 146],

            // ShB
            EClassJob.Gunbreaker => [84],
            EClassJob.Dancer => [121],

            // EW
            EClassJob.Sage => [152],
            EClassJob.Reaper => [153],

            // DT
            EClassJob.Viper => [176],
            EClassJob.Pictomancer => [177],

            // Crafter
            EClassJob.Alchemist => [48, 49, 50],
            EClassJob.Armorer => [36, 37, 38],
            EClassJob.Blacksmith => [33, 34, 35],
            EClassJob.Carpenter => [30, 31, 32],
            EClassJob.Culinarian => [51, 52, 53],
            EClassJob.Goldsmith => [39, 40, 41],
            EClassJob.Leatherworker => [42, 43, 44],
            EClassJob.Weaver => [45, 46, 47],

            // Gatherer
            EClassJob.Miner => [54, 55, 56],
            EClassJob.Botanist => [57, 58, 59],
            EClassJob.Fisher => [60, 61, 62],

            _ => throw new ArgumentOutOfRangeException(nameof(classJob)),
        };

        chapterIds.AddRange(classJob switch
        {
            _ when classJob.IsTank() => TankRoleQuests,
            _ when classJob.IsHealer() => HealerRoleQuests,
            _ when classJob.IsMelee() => MeleeRoleQuests,
            _ when classJob.IsPhysicalRanged() => PhysicalRangedRoleQuests,
            _ when classJob.IsCaster() && classJob != EClassJob.BlueMage => CasterRoleQuests,
            _ => []
        });

        return GetQuestsInNewGamePlusChapters(chapterIds);
    }

    private List<QuestInfo> GetQuestsInNewGamePlusChapters(List<uint> chapterIds)
    {
        return _quests.Values
            .Where(x => x is QuestInfo)
            .Cast<QuestInfo>()
            .Where(x => chapterIds.Contains(x.NewGamePlusChapter))
            .ToList();
    }

    public List<QuestId> GetLockedClassQuests()
    {
        EClassJob startingClass;
        unsafe
        {
            var playerState = PlayerState.Instance();
            if (playerState != null)
                startingClass = (EClassJob)playerState->FirstClass;
            else
                startingClass = EClassJob.Adventurer;
        }

        if (startingClass == EClassJob.Adventurer)
            return [];

        // If you start the game as another class, you get:
        // - "So you want to be a XX"
        // - "Way of the XX" (depends on "So you want to be a XX")
        // - "My First XX"
        // If you start the game with this class, you get:
        // - "Way of the XX" (no preconditions)
        // In both cases, the level 10 quests are different
        List<List<ushort>> startingClassQuests =
        [
            startingClass == EClassJob.Gladiator ? [177, 285, 286, 288] : [253, 261],
            startingClass == EClassJob.Pugilist ? [178, 532, 553, 698] : [533, 555],
            startingClass == EClassJob.Marauder ? [179, 310, 312, 315] : [311, 314],
            startingClass == EClassJob.Lancer ? [180, 132, 218, 143] : [23, 35],
            startingClass == EClassJob.Archer ? [181, 131, 219, 134] : [21, 67],
            startingClass == EClassJob.Conjurer ? [182, 133, 211, 147] : [22, 91],
            startingClass == EClassJob.Thaumaturge ? [183, 344, 346, 349] : [345, 348],
            startingClass == EClassJob.Arcanist ? [451, 452, 454, 457] : [453, 456],
        ];
        return startingClassQuests.SelectMany(x => x).Select(x => new QuestId(x)).ToList();
    }
}
