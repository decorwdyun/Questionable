using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Model;

namespace Questionable.Windows.QuestComponents;

internal sealed class QuestTooltipComponent
{
    private readonly QuestRegistry _questRegistry;
    private readonly QuestData _questData;
    private readonly TerritoryData _territoryData;
    private readonly GameFunctions _gameFunctions;
    private readonly UiUtils _uiUtils;

    public QuestTooltipComponent(
        QuestRegistry questRegistry,
        QuestData questData,
        TerritoryData territoryData,
        GameFunctions gameFunctions,
        UiUtils uiUtils)
    {
        _questRegistry = questRegistry;
        _questData = questData;
        _territoryData = territoryData;
        _gameFunctions = gameFunctions;
        _uiUtils = uiUtils;
    }

    public void Draw(QuestInfo quest)
    {
        using var tooltip = ImRaii.Tooltip();
        if (tooltip)
        {
            var (color, _, tooltipText) = _uiUtils.GetQuestStyle(quest.QuestId);
            ImGui.TextColored(color, tooltipText);
            if (quest.IsRepeatable)
            {
                ImGui.SameLine();
                ImGui.TextUnformatted("Repeatable");
            }

            if (quest.CompletesInstantly)
            {
                ImGui.SameLine();
                ImGui.TextUnformatted("Instant");
            }

            if (!_questRegistry.IsKnownQuest(quest.QuestId))
            {
                ImGui.SameLine();
                ImGui.TextUnformatted("NoQuestPath");
            }

            DrawQuestUnlocks(quest, 0);
        }
    }

    private void DrawQuestUnlocks(QuestInfo quest, int counter)
    {
        if (counter >= 10)
            return;

        if (counter != 0 && quest.IsMainScenarioQuest)
            return;

        if (counter > 0)
            ImGui.Indent();

        if (quest.PreviousQuests.Count > 0)
        {
            if (counter == 0)
                ImGui.Separator();

            if (quest.PreviousQuests.Count > 1)
            {
                if (quest.PreviousQuestJoin == QuestInfo.QuestJoin.All)
                    ImGui.Text("Requires all:");
                else if (quest.PreviousQuestJoin == QuestInfo.QuestJoin.AtLeastOne)
                    ImGui.Text("Requires one:");
            }

            foreach (var q in quest.PreviousQuests)
            {
                var qInfo = _questData.GetQuestInfo(q);
                var (iconColor, icon, _) = _uiUtils.GetQuestStyle(q);
                if (!_questRegistry.IsKnownQuest(qInfo.QuestId))
                    iconColor = ImGuiColors.DalamudGrey;

                _uiUtils.ChecklistItem(FormatQuestUnlockName(qInfo), iconColor, icon);

                if (counter <= 2 || icon != FontAwesomeIcon.Check)
                    DrawQuestUnlocks(qInfo, counter + 1);
            }
        }

        if (counter == 0 && quest.QuestLocks.Count > 0)
        {
            ImGui.Separator();
            if (quest.QuestLocks.Count > 1)
            {
                if (quest.QuestLockJoin == QuestInfo.QuestJoin.All)
                    ImGui.Text("Blocked by (if all completed):");
                else if (quest.QuestLockJoin == QuestInfo.QuestJoin.AtLeastOne)
                    ImGui.Text("Blocked by (if at least completed):");
            }
            else
                ImGui.Text("Blocked by (if completed):");

            foreach (var q in quest.QuestLocks)
            {
                var qInfo = _questData.GetQuestInfo(q);
                var (iconColor, icon, _) = _uiUtils.GetQuestStyle(q);
                if (!_questRegistry.IsKnownQuest(qInfo.QuestId))
                    iconColor = ImGuiColors.DalamudGrey;

                _uiUtils.ChecklistItem(FormatQuestUnlockName(qInfo), iconColor, icon);
            }
        }

        if (counter == 0 && quest.PreviousInstanceContent.Count > 0)
        {
            ImGui.Separator();
            if (quest.PreviousInstanceContent.Count > 1)
            {
                if (quest.PreviousQuestJoin == QuestInfo.QuestJoin.All)
                    ImGui.Text("Requires all:");
                else if (quest.PreviousQuestJoin == QuestInfo.QuestJoin.AtLeastOne)
                    ImGui.Text("Requires one:");
            }
            else
                ImGui.Text("Requires:");

            foreach (var instanceId in quest.PreviousInstanceContent)
            {
                string instanceName = _territoryData.GetInstanceName(instanceId) ?? "?";
                var (iconColor, icon) = UiUtils.GetInstanceStyle(instanceId);
                _uiUtils.ChecklistItem(instanceName, iconColor, icon);
            }
        }

        if (counter == 0 && quest.GrandCompany != GrandCompany.None)
        {
            ImGui.Separator();
            string gcName = quest.GrandCompany switch
            {
                GrandCompany.Maelstrom => "Maelstrom",
                GrandCompany.TwinAdder => "Twin Adder",
                GrandCompany.ImmortalFlames => "Immortal Flames",
                _ => "None",
            };

            GrandCompany currentGrandCompany = _gameFunctions.GetGrandCompany();
            _uiUtils.ChecklistItem($"Grand Company: {gcName}", quest.GrandCompany == currentGrandCompany);
        }

        if (counter > 0)
            ImGui.Unindent();
    }

    private static string FormatQuestUnlockName(QuestInfo questInfo)
    {
        if (questInfo.IsMainScenarioQuest)
            return $"{questInfo.Name} ({questInfo.QuestId}, MSQ)";
        else
            return $"{questInfo.Name} ({questInfo.QuestId})";
    }
}
