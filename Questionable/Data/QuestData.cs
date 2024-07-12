using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using Questionable.Model;
using Quest = Lumina.Excel.GeneratedSheets.Quest;

namespace Questionable.Data;

internal sealed class QuestData
{
    private readonly ITargetManager _targetManager;
    private readonly IChatGui _chatGui;

    private readonly ImmutableDictionary<ushort, QuestInfo> _quests;

    public QuestData(IDataManager dataManager, ITargetManager targetManager, IChatGui chatGui)
    {
        _targetManager = targetManager;
        _chatGui = chatGui;

        _quests = dataManager.GetExcelSheet<Quest>()!
            .Where(x => x.RowId > 0)
            .Where(x => x.IssuerLocation.Row > 0)
            .Select(x => new QuestInfo(x))
            .ToImmutableDictionary(x => x.QuestId, x => x);
    }

    public QuestInfo GetQuestInfo(ushort questId)
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

    public void ShowQuestsIssuedByTarget()
    {
        var targetId = _targetManager.Target?.DataId;
        if (targetId == null)
        {
            _chatGui.PrintError("[Questionable] No target selected.");
            return;
        }

        List<QuestInfo> quests = GetAllByIssuerDataId(targetId.Value);
        _chatGui.Print($"{quests.Count} quest(s) issued by target {_targetManager.Target?.Name}:");

        foreach (QuestInfo quest in quests)
            _chatGui.Print($"  {quest.QuestId}_{quest.SimplifiedName}");
    }
}
