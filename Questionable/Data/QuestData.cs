using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets;
using Questionable.Model;
using Questionable.Model.Questing;
using Quest = Lumina.Excel.GeneratedSheets.Quest;

namespace Questionable.Data;

internal sealed class QuestData
{
    private readonly Dictionary<ElementId, IQuestInfo> _quests;

    public QuestData(IDataManager dataManager)
    {
        List<IQuestInfo> quests =
        [
            ..dataManager.GetExcelSheet<Quest>()!
                .Where(x => x.RowId > 0)
                .Where(x => x.IssuerLocation.Row > 0)
                .Where(x => x.Festival.Row == 0)
                .Select(x => new QuestInfo(x)),
            ..dataManager.GetExcelSheet<SatisfactionNpc>()!
                .Where(x => x.RowId > 0)
                .Select(x => new SatisfactionSupplyInfo(x))
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
            .Where(x => x is QuestInfo)
            .Cast<QuestInfo>()
            .Where(x => x.JournalGenre == journalGenre)
            .OrderBy(x => x.SortKey)
            .ThenBy(x => x.QuestId)
            .ToList();
    }
}
