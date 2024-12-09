using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Questionable.Controller;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

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
        using var popup = ImRaii.ContextPopup($"##QuestPopup{questInfo.QuestId}", ImGuiPopupFlags.MouseButtonRight);
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
}
