using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Model;
using Questionable.Windows.QuestComponents;
using static Questionable.Utils.LocalizeShortcut;
namespace Questionable.Windows.JournalComponents;

internal sealed class QuestRewardComponent
(
    QuestRegistry questRegistry,
    QuestData questData,
    QuestTooltipComponent questTooltipComponent,
    UiUtils uiUtils)
{
    private bool _showEventRewards;

    public void DrawItemRewards()
    {
        using ImRaii.TabItemDisposable tab = ImRaii.TabItem(_L("物品奖励"));
        if (!tab)
            return;

        ImGui.Checkbox(_L("显示季节活动任务奖励"), ref _showEventRewards);
        ImGui.Spacing();

        ImGui.BulletText(
            _L("仅列出不可交易物品（例如飞艇模型可在市场交易，因此不会列出）。"));

        DrawGroup(_L("坐骑"), EItemRewardType.Mount);
        DrawGroup(_L("宠物"), EItemRewardType.Minion);
        DrawGroup(_L("管弦乐琴乐谱"), EItemRewardType.OrchestrionRoll);
        DrawGroup(_L("幻卡"), EItemRewardType.TripleTriadCard);
        DrawGroup(_L("时尚配饰"), EItemRewardType.FashionAccessory);
    }

    private void DrawGroup(string label, EItemRewardType type)
    {
        if (!ImGui.CollapsingHeader($"{label}###Reward{type}"))
            return;

        var results = questData.RedeemableItems.Where(x => x.Type == type)
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        if (results.Count == 0)
            ImGui.Text("No results");

        foreach (ItemReward item in results)
        {
            if (questData.TryGetQuestInfo(item.ElementId, out IQuestInfo? questInfo))
            {
                bool isEventQuest = questInfo is QuestInfo { IsSeasonalEvent: true };
                if (!_showEventRewards && isEventQuest)
                    continue;

                string name = item.Name;
                if (isEventQuest)
                    name += $" {SeIconChar.Clock.ToIconString()}";

                bool complete = item.IsUnlocked();
                Vector4 color = !questRegistry.IsKnownQuest(item.ElementId)
                    ? ImGuiColors.DalamudGrey
                    : complete
                        ? ImGuiColors.ParsedGreen
                        : ImGuiColors.DalamudRed;
                FontAwesomeIcon icon = complete ? FontAwesomeIcon.Check : FontAwesomeIcon.Times;
                if (uiUtils.ChecklistItem(name, color, icon))
                {
                    using ImRaii.TooltipDisposable tooltip = ImRaii.Tooltip();
                    if (item.Type is not EItemRewardType.TripleTriadCard)
                        ImGui.Text(_LF("Obtained from: {0}", questInfo.Name));
                    using (ImRaii.PushIndent())
                    {
                        questTooltipComponent.DrawInner(questInfo, false);
                    }
                }
            }
        }
    }
}
