using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Windows.QuestComponents;

namespace Questionable.Windows.JournalComponents;

internal sealed class AlliedSocietyJournalComponent
{
    private static readonly string[] RankNames =
        ["Neutral", "Recognized", "Friendly", "Trusted", "Respected", "Honored", "Sworn", "Allied"];

    private readonly QuestFunctions _questFunctions;
    private readonly AlliedSocietyQuestFunctions _alliedSocietyQuestFunctions;
    private readonly QuestData _questData;
    private readonly QuestRegistry _questRegistry;
    private readonly QuestJournalUtils _questJournalUtils;
    private readonly QuestTooltipComponent _questTooltipComponent;
    private readonly UiUtils _uiUtils;

    public AlliedSocietyJournalComponent(
        QuestFunctions questFunctions,
        AlliedSocietyQuestFunctions alliedSocietyQuestFunctions,
        QuestData questData,
        QuestRegistry questRegistry,
        QuestJournalUtils questJournalUtils,
        QuestTooltipComponent questTooltipComponent,
        UiUtils uiUtils)
    {
        _questFunctions = questFunctions;
        _alliedSocietyQuestFunctions = alliedSocietyQuestFunctions;
        _questData = questData;
        _questRegistry = questRegistry;
        _questJournalUtils = questJournalUtils;
        _questTooltipComponent = questTooltipComponent;
        _uiUtils = uiUtils;
    }

    public void DrawAlliedSocietyQuests()
    {
        using var tab = ImRaii.TabItem("Allied Societies");
        if (!tab)
            return;

        foreach (EAlliedSociety alliedSociety in Enum.GetValues<EAlliedSociety>().Where(x => x != EAlliedSociety.None))
        {
            List<QuestInfo> quests = _alliedSocietyQuestFunctions.GetAvailableAlliedSocietyQuests(alliedSociety)
                .Select(x => (QuestInfo)_questData.GetQuestInfo(x))
                .ToList();
            if (quests.Count == 0)
                continue;

            bool containsNewQuests = quests.Any(x => !_questFunctions.IsQuestComplete(x.QuestId));
            if (containsNewQuests)
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudYellow);

            if (!ImGui.CollapsingHeader($"{alliedSociety}###AlliedSociety{(int)alliedSociety}"))
                continue;

            if (containsNewQuests)
                ImGui.PopStyleColor();

            if (alliedSociety <= EAlliedSociety.Ixal)
            {
                for (byte i = 1; i <= 8; ++i)
                {
                    var questsByRank = quests.Where(x => x.AlliedSocietyRank == i).ToList();
                    if (questsByRank.Count == 0)
                        continue;

                    ImGui.Text(RankNames[i - 1]);
                    foreach (var quest in questsByRank)
                        DrawQuest(quest);
                }
            }
            else
            {
                foreach (var quest in quests)
                    DrawQuest(quest);
            }
        }
    }

    private void DrawQuest(QuestInfo questInfo)
    {
        var (color, icon, tooltipText) = _uiUtils.GetQuestStyle(questInfo.QuestId);
        if (!_questRegistry.TryGetQuest(questInfo.QuestId, out var quest))
            color = ImGuiColors.DalamudGrey;

        if (_uiUtils.ChecklistItem($"{questInfo.Name} ({tooltipText})", color, icon))
            _questTooltipComponent.Draw(questInfo);

        _questJournalUtils.ShowContextMenu(questInfo, quest, nameof(AlliedSocietyJournalComponent));
    }
}
