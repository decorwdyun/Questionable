using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Questionable.Controller;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;
using System;
using Dalamud.Interface.Colors;

namespace Questionable.Windows.JournalComponents;

internal sealed class QuestJournalUtils
{
    private readonly QuestController _questController;
    private readonly QuestFunctions _questFunctions;
    private readonly ICommandManager _commandManager;

    public QuestJournalUtils(QuestController questController, QuestFunctions questFunctions,
        ICommandManager commandManager)
    {
        _questController = questController;
        _questFunctions = questFunctions;
        _commandManager = commandManager;
    }

    public void ShowContextMenu(IQuestInfo questInfo, Quest? quest, string label)
    {
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            ImGui.OpenPopup($"##QuestPopup{questInfo.QuestId}");

        using var popup = ImRaii.Popup($"##QuestPopup{questInfo.QuestId}");
        if (!popup)
            return;

        if (ImGui.MenuItem("开始任务", _questFunctions.IsReadyToAcceptQuest(questInfo.QuestId)))
        {
            _questController.SetNextQuest(quest);
            _questController.Start(label);
        }

        bool openInQuestMap = _commandManager.Commands.TryGetValue("/questinfo", out var commandInfo);
        if (ImGui.MenuItem("在 Quest Map 中打开", questInfo.QuestId is QuestId && openInQuestMap))
        {
            _commandManager.DispatchCommand("/questinfo", questInfo.QuestId.ToString() ?? string.Empty,
                commandInfo!);
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
}
