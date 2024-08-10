using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using LLib.GameData;
using Lumina.Excel.GeneratedSheets;
using Questionable.Model;
using Questionable.Model.Questing;
using Quest = Lumina.Excel.GeneratedSheets.Quest;

namespace Questionable.Data;

internal sealed class QuestData
{
    public static readonly IReadOnlyList<QuestId> CrystalTowerQuests =
        [new(1709), new(1200), new(1201), new(1202), new(1203), new(1474), new(494), new(495)];
    private readonly Dictionary<ElementId, IQuestInfo> _quests;

    public QuestData(IDataManager dataManager)
    {
        Dictionary<uint, ushort> questChapters =
            dataManager.GetExcelSheet<QuestChapter>()!
                .Where(x => x.RowId > 0 && x.Quest.Row > 0)
                .ToDictionary(x => x.Quest.Row, x => x.Redo);

        List<IQuestInfo> quests =
        [
            ..dataManager.GetExcelSheet<Quest>()!
                .Where(x => x.RowId > 0)
                .Where(x => x.IssuerLocation.Row > 0)
                .Select(x => new QuestInfo(x, questChapters.GetValueOrDefault(x.RowId))),
            ..dataManager.GetExcelSheet<SatisfactionNpc>()!
                .Where(x => x.RowId > 0)
                .Select(x => new SatisfactionSupplyInfo(x)),
            ..dataManager.GetExcelSheet<Leve>()!
                .Where(x => x.RowId > 0)
                .Where(x => x.LevelLevemete.Row != 0)
                .Select(x => new LeveInfo(x)),
        ];
        _quests = quests.ToDictionary(x => x.QuestId, x => x);
    }

    public IQuestInfo GetQuestInfo(ElementId elementId)
    {
        return _quests[elementId] ?? throw new ArgumentOutOfRangeException(nameof(elementId));
    }

    public List<IQuestInfo> GetAllByIssuerDataId(uint targetId)
    {
        return _quests.Values
            .Where(x => x.IssuerDataId == targetId)
            .ToList();
    }

    public bool IsIssuerOfAnyQuest(uint targetId) => _quests.Values.Any(x => x.IssuerDataId == targetId);

    public List<QuestInfo> GetAllByJournalGenre(uint journalGenre)
    {
        return _quests.Values
            .Where(x => x is QuestInfo { IsSeasonalEvent: false })
            .Cast<QuestInfo>()
            .Where(x => x.JournalGenre == journalGenre)
            .OrderBy(x => x.SortKey)
            .ThenBy(x => x.QuestId)
            .ToList();
    }

    public List<QuestInfo> GetClassJobQuests(EClassJob classJob)
    {
        List<ushort> chapterIds = classJob switch
        {
            EClassJob.Adventurer => throw new ArgumentOutOfRangeException(nameof(classJob)),

            // ARR
            EClassJob.Gladiator => [63],
            EClassJob.Paladin => [72, 73, 74, 75],
            EClassJob.Marauder => [64],
            EClassJob.Warrior => [76, 77, 78, 79],
            EClassJob.Conjurer => [65],
            EClassJob.WhiteMage => [86, 87, 88, 89],
            EClassJob.Arcanist => [66],
            EClassJob.Summoner => [127, 128, 129, 130],
            EClassJob.Scholar => [90, 91, 92, 93],
            EClassJob.Pugilist => [67],
            EClassJob.Monk => [98, 99, 100, 101],
            EClassJob.Lancer => [68],
            EClassJob.Dragoon => [102, 103, 104, 105],
            EClassJob.Rogue => [69],
            EClassJob.Ninja => [106, 107, 108, 109],
            EClassJob.Archer => [70],
            EClassJob.Bard => [113, 114, 115, 116],
            EClassJob.Thaumaturge => [71],
            EClassJob.BlackMage => [123, 124, 125, 126],

            // HW
            EClassJob.DarkKnight => [80, 81, 82, 83],
            EClassJob.Astrologian => [94, 95, 96, 97],
            EClassJob.Machinist => [117, 118, 119, 120],

            // SB
            EClassJob.Samurai => [110, 111, 112],
            EClassJob.RedMage => [131, 132, 133],
            EClassJob.BlueMage => [134, 135, 146, 170],

            // ShB
            EClassJob.Gunbreaker => [84, 85],
            EClassJob.Dancer => [121, 122],

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
            _ when classJob.IsTank() => [136, 154, 178],
            _ when classJob.IsHealer() => [137, 155, 179],
            _ when classJob.IsMelee() => [138, 156, 180],
            _ when classJob.IsPhysicalRanged() => [138, 157, 181],
            _ when classJob.IsCaster() && classJob != EClassJob.BlueMage => [139, 158, 182],
            _ => []
        });

        return GetQuestsInNewGamePlusChapters(chapterIds);
    }

    private List<QuestInfo> GetQuestsInNewGamePlusChapters(List<ushort> chapterIds)
    {
        return _quests.Values
            .Where(x => x is QuestInfo)
            .Cast<QuestInfo>()
            .Where(x => chapterIds.Contains(x.NewGamePlusChapter))
            .ToList();
    }
}
