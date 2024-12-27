using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Questionable.Controller;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;
using System;

namespace Questionable.Windows.JournalComponents;

internal sealed class QuestJournalUtils
{
    private readonly QuestController _questController;
    private readonly QuestFunctions _questFunctions;
    private readonly ICommandManager _commandManager;

    public static bool AvailableOnly;
    public static bool HideNoPaths;

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

        if (ImGui.MenuItem("Start as next quest", _questFunctions.IsReadyToAcceptQuest(questInfo.QuestId)))
        {
            _questController.SetNextQuest(quest);
            _questController.Start(label);
        }

        bool openInQuestMap = _commandManager.Commands.TryGetValue("/questinfo", out var commandInfo);
        if (ImGui.MenuItem("View in Quest Map", questInfo.QuestId is QuestId && openInQuestMap))
        {
            _commandManager.DispatchCommand("/questinfo", questInfo.QuestId.ToString() ?? string.Empty,
                commandInfo!);
        }
    }

    internal static void ShowFilterContextMenu(QuestJournalComponent journalUI)
    {
        if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.Filter))
            ImGui.OpenPopup($"##QuestFilters");

        using var popup = ImRaii.Popup($"##QuestFilters");
        if (!popup)
            return;

        if (ImGui.Checkbox("Show only Available Quests", ref AvailableOnly) ||
            ImGui.Checkbox("Hide Quests Without Path", ref HideNoPaths))
            journalUI.UpdateFilter();


    }
}
