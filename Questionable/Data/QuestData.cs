using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Dalamud.Plugin.Services;
using Questionable.Model;
using Questionable.Model.Questing;
using Quest = Lumina.Excel.GeneratedSheets.Quest;

namespace Questionable.Data;

internal sealed class QuestData
{
    private readonly ImmutableDictionary<QuestId, QuestInfo> _quests;

    public QuestData(IDataManager dataManager)
    {
        _quests = dataManager.GetExcelSheet<Quest>()!
            .Where(x => x.RowId > 0)
            .Where(x => x.IssuerLocation.Row > 0)
            .Where(x => x.Festival.Row == 0)
            .Select(x => new QuestInfo(x))
            .ToImmutableDictionary(x => x.QuestId, x => x);
    }

    public QuestInfo GetQuestInfo(ElementId elementId)
    {
        if (elementId is QuestId questId)
            return GetQuestInfo(questId);

        throw new ArgumentException("Invalid id", nameof(elementId));
    }

    public QuestInfo GetQuestInfo(QuestId questId)
    {
        return _quests[questId] ?? throw new ArgumentOutOfRangeException(nameof(questId));
    }

    public List<QuestInfo> GetAllByIssuerDataId(uint targetId)
    {
        return _quests.Values
            .Where(x => x.IssuerDataId == targetId)
            .ToList();
    }

    public bool IsIssuerOfAnyQuest(uint targetId) => _quests.Values.Any(x => x.IssuerDataId == targetId);

    public List<QuestInfo> GetAllByJournalGenre(uint journalGenre)
    {
        return _quests.Values
            .Where(x => x.JournalGenre == journalGenre)
            .OrderBy(x => x.SortKey)
            .ThenBy(x => x.QuestId)
            .ToList();
    }
}
