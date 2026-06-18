using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Utils;
using Questionable.Windows.QuestComponents;
using static Questionable.Utils.LocalizeShortcut;
namespace Questionable.Windows.JournalComponents;

internal sealed class AlliedSocietyJournalComponent
(
    QuestFunctions questFunctions,
    QuestController questController,
    AlliedSocietyQuestFunctions alliedSocietyQuestFunctions,
    QuestData questData,
    QuestRegistry questRegistry,
    QuestJournalUtils questJournalUtils,
    QuestTooltipComponent questTooltipComponent,
    Configuration configuration,
    IDalamudPluginInterface pluginInterface,
    UiUtils uiUtils
)
{
    private static readonly Dictionary<EAlliedSociety, string> SocietyNames = new()
    {
        [EAlliedSociety.Amaljaa] = "蜥蜴人族",
        [EAlliedSociety.Sylphs] = "妖精族",
        [EAlliedSociety.Kobolds] = "地灵族",
        [EAlliedSociety.Sahagin] = "鱼人族",
        [EAlliedSociety.Ixal] = "鸟人族",
        [EAlliedSociety.VanuVanu] = "瓦努族",
        [EAlliedSociety.Vath] = "骨颌族",
        [EAlliedSociety.Moogles] = "莫古力族",
        [EAlliedSociety.Kojin] = "甲人族",
        [EAlliedSociety.Ananta] = "阿难陀族",
        [EAlliedSociety.Namazu] = "鲶鱼精族",
        [EAlliedSociety.Pixies] = "仙子族",
        [EAlliedSociety.Qitari] = "奇塔利族",
        [EAlliedSociety.Dwarves] = "矮人族",
        [EAlliedSociety.Arkasodara] = "象魔族",
        [EAlliedSociety.Omicrons] = "奥密克戎族",
        [EAlliedSociety.Loporrits] = "兔兔族",
        [EAlliedSociety.Pelupelu] = "佩鲁佩鲁族",
        [EAlliedSociety.MamoolJa] = "辉鳞族",
        [EAlliedSociety.YokHuy] = "尤卡巨人族",
    };
    private static readonly Dictionary<EAlliedSocietyRank, string> RankNames = new()
    {
        [EAlliedSocietyRank.None] = "无",
        [EAlliedSocietyRank.Neutral] = "中立",
        [EAlliedSocietyRank.Recognized] = "承认",
        [EAlliedSocietyRank.Friendly] = "友好",
        [EAlliedSocietyRank.Trusted] = "信赖",
        [EAlliedSocietyRank.Respected] = "尊敬",
        [EAlliedSocietyRank.Honored] = "名誉",
        [EAlliedSocietyRank.Sworn] = "誓约",
        [EAlliedSocietyRank.Allied] = "血誓",
    };

    uint _unchecked;
    uint _incomplete;

    public void DrawAlliedSocietyQuests()
    {
        using ImRaii.TabItemDisposable tab = ImRaii.TabItem(_L("友好部族"));
        if (!tab)
            return;
        bool addPending = false;

        if (ImGuiComponentsLocal.IconButtonWithText(FontAwesomeIcon.Plus, _L("添加")))
            addPending = true;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(_L("添加未检查的任务（来自已展开的分类）到优先队列"));
        ImGui.SameLine();

        bool preventQuestCompletion = configuration.Advanced.PreventQuestCompletion;
        bool abandonQuestBeforeCompletion = configuration.Advanced.AbandonQuestBeforeCompletion;
        bool removeFromPriorityWhenAbandoned = configuration.Advanced.RemoveFromPriorityWhenAbandoned;
        if (ImGuiComponentsLocal.IconButton(FontAwesomeIcon.Stop, preventQuestCompletion ? ImGuiColors.DalamudOrange : null))
        {
            configuration.Advanced.PreventQuestCompletion = !preventQuestCompletion;
            pluginInterface.SavePluginConfig(configuration);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(_L("阻止任务完成"));

        ImGui.SameLine();
        if (ImGuiComponentsLocal.IconButton(FontAwesomeIcon.Ban, abandonQuestBeforeCompletion ? ImGuiColors.DalamudOrange : null))
        {
            configuration.Advanced.AbandonQuestBeforeCompletion = !abandonQuestBeforeCompletion;
            pluginInterface.SavePluginConfig(configuration);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(_L("完成前放弃任务"));

        ImGui.SameLine();
        if (ImGuiComponentsLocal.IconButton(FontAwesomeIcon.Trash, removeFromPriorityWhenAbandoned ? ImGuiColors.DalamudOrange : null))
        {
            configuration.Advanced.RemoveFromPriorityWhenAbandoned = !removeFromPriorityWhenAbandoned;
            pluginInterface.SavePluginConfig(configuration);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(_L("放弃时从优先队列移除"));

        ImGui.SameLine();

        unsafe
        {
            uint allowances = QuestManager.Instance()->GetBeastTribeAllowance();
            ImGui.Text(_LF("剩余配额: {0}/12", allowances));
        }

        if (_incomplete > 0)
        {
            ImGui.SameLine();
            ImGuiComponents.HelpMarker(_LF("黄色标记的任务表示此角色从未完成过。"),
                                       FontAwesomeIcon.InfoCircle, ImGuiColors.DalamudYellow);
        }

        if (_unchecked > 0)
        {
            ImGui.SameLine();
            ImGuiComponents.HelpMarker(_L("橙色标记的任务需要通过 LastChecked 系统报告为可用或不可用。\n请联系 Aly 了解更多详情！"),
                                       FontAwesomeIcon.InfoCircle, ImGuiColors.DalamudOrange);
            ImGui.SameLine();
            ImGui.Text(_LF("未检查: {0}", _unchecked));
        }

        _unchecked = 0;
        _incomplete = 0;

        foreach (EAlliedSociety alliedSociety in Enum.GetValues<EAlliedSociety>().Where(x => x != EAlliedSociety.None))
        {
            List<IQuestInfo> quests = alliedSocietyQuestFunctions.GetAvailableAlliedSocietyQuests(alliedSociety)
                .Select(x => questData.GetQuestInfo(x))
                .ToList();
            (EAlliedSocietyRank rank, ushort currentRep, ushort neededRep) = questFunctions.GetAlliedSocietyRankAndRep(alliedSociety);

            string rankName = RankNames.GetValueOrDefault(rank, rank.ToString());
            string rep = neededRep != 0 ? $"({rankName} {currentRep}/{neededRep}) " : "";

            string label = $"{rep}{SocietyNames.GetValueOrDefault(alliedSociety, alliedSociety.ToString())}###AlliedSociety{(int)alliedSociety}";
            bool isOpen;

            using (ImRaii.Disabled(quests.Count == 0))
            {
                // If, of the quests in this category, any quest...
                // if (quests.Any(x => !x.QuestId.Value.Equals(1569) && (
                //         !questRegistry.TryGetQuest(x.QuestId, out Quest? quest) ||
                //         (quest.Root.Disabled && quest.Root.Comment == null) ||
                //         (quest.Root.LastChecked.Date != null &&
                //             quest.Root.LastChecked.Since(DateTime.Now)!.Value.TotalDays > 30 &&
                //             !(quest.Root.Comment ?? "").Contains("FATE")
                //         )
                //     )
                // ))
                // {
                //     using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudOrange))
                //     {
                //         isOpen = ImGui.CollapsingHeader(label);
                //     }
                //     _unchecked += 1;
                // }
                // else if (quests.Any(x => !questFunctions.IsQuestComplete(x.QuestId))) // if the character has not completed a quest in this category
                if (quests.Any(x => !questFunctions.IsQuestComplete(x.QuestId)))
                {
                    using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudYellow))
                    {
                        isOpen = ImGui.CollapsingHeader(label);
                    }
                }
                else
                {
                    isOpen = ImGui.CollapsingHeader(label);
                }
            }

            questJournalUtils.ShowQuestGroupContextMenu($"DrawAlliedSocietyQuests{alliedSociety}", quests);

            if (!isOpen)
                continue;

            if (alliedSociety <= EAlliedSociety.Ixal)
            {
                for (byte i = 1; i <= 8; ++i)
                {
                    List<IQuestInfo> questsByRank = quests.Where(quest => (byte)((QuestInfo)quest).AlliedSocietyRank == i && !quest.QuestId.Value.Equals(1569)).ToList();
                    if (questsByRank.Count == 0)
                        continue;

                    ImGui.Text($"{RankNames.GetValueOrDefault((EAlliedSocietyRank)i, $"{(EAlliedSocietyRank)i}")}");
                    questJournalUtils.ShowQuestGroupContextMenu($"DrawAlliedSocietyQuests{alliedSociety}/{(EAlliedSocietyRank)i}", questsByRank);
                    foreach (IQuestInfo quest in questsByRank)
                        DrawQuest((QuestInfo)quest, addPending, neededRep != 0);
                }
            }
            else
            {
                foreach (IQuestInfo quest in quests)
                    DrawQuest((QuestInfo)quest, addPending, neededRep != 0);
            }
        }
    }

    private void DrawQuest(QuestInfo questInfo, bool addPending = false, bool showRepValue = false)
    {
        (Vector4 color, FontAwesomeIcon icon, string tooltipText) = uiUtils.GetQuestStyle(questInfo.QuestId);
        bool fate = false;
        string lastChecked = "";
        if (!questRegistry.TryGetQuest(questInfo.QuestId, out Quest? quest))
            color = ImGuiColors.DalamudGrey;
        else
        {
            if (quest.Root.LastChecked.Date != null)
            {
                lastChecked = $"({quest.Root.LastChecked.Date})";
                if (quest.Root.LastChecked.Since(DateTime.Now)!.Value.TotalDays > 30)
                    color = ImGuiColors.DalamudOrange;
            }
            else
                color = ImGuiColors.ParsedOrange;
            if (quest.Root.Disabled && (quest.Root.Comment ?? "").Contains("FATE"))
            {
                color = ImGuiColors.DalamudRed;
                fate = true;
            }
        }

        string checklistItem = $"{questInfo.Name} ({tooltipText}) {lastChecked}";
        if (fate)
            checklistItem = "(FATE) " + checklistItem;
        if (showRepValue)
            checklistItem = $"[+{questInfo.SocietyRepValue}] " + checklistItem;
        if (uiUtils.ChecklistItem(checklistItem, color, icon))
            questTooltipComponent.Draw(questInfo);
        if (addPending && (color.Equals(ImGuiColors.DalamudOrange) || color.Equals(ImGuiColors.ParsedOrange)))
            questController.PriorityManager.Add(questInfo.QuestId);

        questJournalUtils.ShowContextMenu(questInfo, quest, nameof(AlliedSocietyJournalComponent));

        if (quest != null && questController.PriorityManager.Contains(quest))
        {
            ImGui.SameLine();
            using (pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
                ImGui.TextColored(ImGuiColors.DalamudYellow, FontAwesomeIcon.ExclamationCircle.ToIconString());
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(_L("此任务在优先队列中。"));
        }
    }
}
