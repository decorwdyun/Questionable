using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Questionable.Controller;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Windows.JournalComponents;

internal sealed class QuestJournalUtils(QuestController questController, QuestFunctions questFunctions,
    ICommandManager commandManager)
{
    private readonly QuestController _questController = questController;
    private readonly QuestFunctions _questFunctions = questFunctions;
    private readonly ICommandManager _commandManager = commandManager;

    public void ShowContextMenu(IQuestInfo questInfo, Quest? quest, string label)
    {
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            ImGui.OpenPopup($"##QuestPopup{questInfo.QuestId}");

        using var popup = ImRaii.Popup($"##QuestPopup{questInfo.QuestId}");
        if (!popup)
            return;

        using (ImRaii.Disabled(quest == null))
        {
            if (ImGui.MenuItem("添加到优先列表") && quest != null)
            {
                _questController.AddQuestPriority(quest.Id);
            }
        }

        using (ImRaii.Disabled(!_questFunctions.IsReadyToAcceptQuest(questInfo.QuestId)))
        {
            if (ImGui.MenuItem("启动任务"))
            {
                _questController.SetNextQuest(quest);
                _questController.Start(label);
            }

            if (ImGui.MenuItem("Set as next quest"))
                _questController.SetNextQuest(quest);
        }
        
        bool openInQuestMap = _commandManager.Commands.ContainsKey("/questinfo");
        using (ImRaii.Disabled(!(questInfo.QuestId is QuestId) || !openInQuestMap))
        {
            if (ImGui.MenuItem("在 Quest Map 中打开"))
            {
                _commandManager.ProcessCommand($"/questinfo {questInfo.QuestId}");
            }
        }
    }

    internal static void ShowFilterContextMenu(QuestJournalComponent journalUi)
    {
        if (ImGuiComponents.IconButtonWithText(Dalamud.Interface.FontAwesomeIcon.Filter, "筛选"))
            ImGui.OpenPopup("##QuestFilters");

        using var popup = ImRaii.Popup("##QuestFilters");
        if (!popup)
            return;

        if (ImGui.Checkbox("只显示可接取的任务", ref journalUi.Filter.AvailableOnly) ||
            ImGui.Checkbox("隐藏尚未支持的任务", ref journalUi.Filter.HideNoPaths))
            journalUi.UpdateFilter();
    }

    public void ShowQuestGroupContextMenu(string note, List<IQuestInfo> quests)
    {
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            ImGui.OpenPopup($"##QuestGroupPopup{note}");

        using var popup = ImRaii.Popup($"##QuestGroupPopup{note}");
        if (!popup)
            return;

        if (ImGui.MenuItem("全部添加到优先列表"))
        {
            foreach (var quest in quests)
            {
                _questController.AddQuestPriority(quest.QuestId);
            }
        }

        if (ImGui.MenuItem("从优先列表清除"))
        {
            foreach (var quest in quests)
            {
                _questController.RemoveQuestPriority(quest.QuestId);
            }
        }
    }
}
