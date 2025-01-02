using System;
using System.Linq;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using Questionable.Data;
using Questionable.Model;
using Questionable.Windows.QuestComponents;

namespace Questionable.Windows.JournalComponents;

internal sealed class QuestRewardComponent
{
    private readonly QuestData _questData;
    private readonly QuestTooltipComponent _questTooltipComponent;
    private readonly UiUtils _uiUtils;

    public QuestRewardComponent(
        QuestData questData,
        QuestTooltipComponent questTooltipComponent,
        UiUtils uiUtils)
    {
        _questData = questData;
        _questTooltipComponent = questTooltipComponent;
        _uiUtils = uiUtils;
    }

    public void DrawItemRewards()
    {
        using var tab = ImRaii.TabItem("Item Rewards");
        if (!tab)
            return;

        ImGui.BulletText("Only untradeable items are listed (you can e.g. sell your Wind-up Airship from the enovy quest).");

        DrawGroup("Mounts", EItemRewardType.Mount);
        DrawGroup("Minions", EItemRewardType.Minion);
        DrawGroup("Orchestrion Rolls", EItemRewardType.OrchestrionRoll);
        DrawGroup("Triple Triad Cards", EItemRewardType.TripleTriadCard);
        DrawGroup("Fashion Accessories", EItemRewardType.FashionAccessory);
    }

    private void DrawGroup(string label, EItemRewardType type)
    {
        if (!ImGui.CollapsingHeader($"{label}###Reward{type}"))
            return;

        foreach (var item in _questData.RedeemableItems.Where(x => x.Type == type)
                     .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            if (_uiUtils.ChecklistItem(item.Name, item.IsUnlocked()))
            {
                if (_questData.TryGetQuestInfo(item.ElementId, out var questInfo))
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
