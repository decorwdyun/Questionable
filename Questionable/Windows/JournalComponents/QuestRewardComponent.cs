using System;
using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Model;
using Questionable.Windows.QuestComponents;

namespace Questionable.Windows.JournalComponents;

internal sealed class QuestRewardComponent
{
    private readonly QuestRegistry _questRegistry;
    private readonly QuestData _questData;
    private readonly QuestTooltipComponent _questTooltipComponent;
    private readonly UiUtils _uiUtils;

    private bool _showEventRewards;

    public QuestRewardComponent(
        QuestRegistry questRegistry,
        QuestData questData,
        QuestTooltipComponent questTooltipComponent,
        UiUtils uiUtils)
    {
        _questRegistry = questRegistry;
        _questData = questData;
        _questTooltipComponent = questTooltipComponent;
        _uiUtils = uiUtils;
    }

    public void DrawItemRewards()
    {
        using var tab = ImRaii.TabItem("物品奖励");
        if (!tab)
            return;

        ImGui.Checkbox("显示季节性活动任务奖励", ref _showEventRewards);
        ImGui.Spacing();

        ImGui.BulletText(
            "仅列出不可交易的物品（例如，金鲶可以在市场板上出售）。");

        DrawGroup("坐骑", EItemRewardType.Mount);
        DrawGroup("宠物", EItemRewardType.Minion);
        DrawGroup("乐谱", EItemRewardType.OrchestrionRoll);
        DrawGroup("幻卡", EItemRewardType.TripleTriadCard);
        DrawGroup("时尚配饰", EItemRewardType.FashionAccessory);
    }

    private void DrawGroup(string label, EItemRewardType type)
    {
        if (!ImGui.CollapsingHeader($"{label}###Reward{type}"))
            return;

        foreach (var item in _questData.RedeemableItems.Where(x => x.Type == type)
                     .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            if (_questData.TryGetQuestInfo(item.ElementId, out var questInfo))
            {
                bool isEventQuest = questInfo is QuestInfo { IsSeasonalEvent: true };
                if (!_showEventRewards && isEventQuest)
                    continue;

                string name = item.Name;
                if (isEventQuest)
                    name += $" {SeIconChar.Clock.ToIconString()}";

                bool complete = item.IsUnlocked();
                var color = !_questRegistry.IsKnownQuest(item.ElementId)
                    ? ImGuiColors.DalamudGrey
                    : complete
                        ? ImGuiColors.ParsedGreen
                        : ImGuiColors.DalamudRed;
                var icon = complete ? FontAwesomeIcon.Check : FontAwesomeIcon.Times;
                if (_uiUtils.ChecklistItem(name, color, icon))
                {
                    using var tooltip = ImRaii.Tooltip();
                    if (!tooltip)
                        continue;

                    ImGui.Text($"Obtained from: {questInfo.Name}");
                    using (ImRaii.PushIndent())
                        _questTooltipComponent.DrawInner(questInfo, false);
                }
            }
        }
    }
}
