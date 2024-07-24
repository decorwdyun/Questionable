using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using LLib.GameUI;
using LLib.ImGui;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Windows;

internal sealed class QuestSelectionWindow : LWindow
{
    private const string WindowId = "###QuestionableQuestSelection";
    private readonly QuestData _questData;
    private readonly IGameGui _gameGui;
    private readonly IChatGui _chatGui;
    private readonly GameFunctions _gameFunctions;
    private readonly QuestController _questController;
    private readonly QuestRegistry _questRegistry;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly TerritoryData _territoryData;
    private readonly IClientState _clientState;
    private readonly UiUtils _uiUtils;

    private List<QuestInfo> _quests = [];
    private List<QuestInfo> _offeredQuests = [];
    private bool _onlyAvailableQuests = true;

    public QuestSelectionWindow(QuestData questData, IGameGui gameGui, IChatGui chatGui, GameFunctions gameFunctions,
        QuestController questController, QuestRegistry questRegistry, IDalamudPluginInterface pluginInterface,
        TerritoryData territoryData, IClientState clientState, UiUtils uiUtils)
        : base($"Quest Selection{WindowId}")
    {
        _questData = questData;
        _gameGui = gameGui;
        _chatGui = chatGui;
        _gameFunctions = gameFunctions;
        _questController = questController;
        _questRegistry = questRegistry;
        _pluginInterface = pluginInterface;
        _territoryData = territoryData;
        _clientState = clientState;
        _uiUtils = uiUtils;

        Size = new Vector2(500, 200);
        SizeCondition = ImGuiCond.Once;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 200),
        };
    }

    public unsafe void OpenForTarget(IGameObject? gameObject)
    {
        if (gameObject != null)
        {
            var targetId = gameObject.DataId;
            var targetName = gameObject.Name.ToString();
            WindowName = $"Quests starting with {targetName} [{targetId}]{WindowId}";

            _quests = _questData.GetAllByIssuerDataId(targetId);
            if (_gameGui.TryGetAddonByName<AddonSelectIconString>("SelectIconString", out var addonSelectIconString))
            {
                var answers = GameUiController.GetChoices(addonSelectIconString);
                _offeredQuests = _quests
                    .Where(x => answers.Any(y => GameUiController.GameStringEquals(x.Name, y)))
                    .ToList();
            }
            else
                _offeredQuests = [];
        }
        else
        {
            _quests = [];
            _offeredQuests = [];
        }

        IsOpen = _quests.Count > 0;
    }

    public unsafe void OpenForCurrentZone()
    {
        var territoryId = _clientState.TerritoryType;
        var territoryName = _territoryData.GetNameAndId(territoryId);
        WindowName = $"Quests starting in {territoryName}{WindowId}";

        _quests = _questRegistry.AllQuests
            .Where(x => x.FindSequence(0)?.FindStep(0)?.TerritoryId == territoryId)
            .Select(x => _questData.GetQuestInfo(x.QuestId))
            .ToList();

        foreach (var unacceptedQuest in Map.Instance()->UnacceptedQuestMarkers)
        {
            ushort questId = (ushort)(unacceptedQuest.ObjectiveId & 0xFFFF);
            if (_quests.All(q => q.QuestId != questId))
                _quests.Add(_questData.GetQuestInfo(questId));
        }

        _offeredQuests = [];
        IsOpen = true;
    }

    public override void OnClose()
    {
        _quests = [];
        _offeredQuests = [];
    }

    public override void Draw()
    {
        if (_offeredQuests.Count != 0)
            ImGui.Checkbox("Only show quests currently offered", ref _onlyAvailableQuests);

        using var table = ImRaii.Table("QuestSelection", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY);
        if (!table)
        {
            ImGui.Text("Not table");
            return;
        }

        float statusIconSize;
        using (var _ = _pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
        {
            statusIconSize = ImGui.CalcTextSize(FontAwesomeIcon.Copy.ToIconString()).X +
                             1 * ImGui.GetStyle().FramePadding.X;
        }

        ImGui.PushFont(UiBuilder.IconFont);
        var actionIconSize = ImGui.CalcTextSize(FontAwesomeIcon.Copy.ToIconString()).X +
                             ImGui.CalcTextSize(FontAwesomeIcon.Copy.ToIconString()).X +
                             ImGui.CalcTextSize(FontAwesomeIcon.Copy.ToIconString()).X +
                             5 * ImGui.GetStyle().FramePadding.X +
                             2 * ImGui.GetStyle().ItemSpacing.X;
        ImGui.PopFont();

        ImGui.TableSetupColumn("Id", ImGuiTableColumnFlags.WidthFixed, 50 * ImGui.GetIO().FontGlobalScale);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, statusIconSize);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.None, 200);
        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, actionIconSize);
        ImGui.TableHeadersRow();

        foreach (QuestInfo quest in (_offeredQuests.Count != 0 && _onlyAvailableQuests) ? _offeredQuests : _quests)
        {
            ImGui.TableNextRow();

            string questId = quest.QuestId.ToString(CultureInfo.InvariantCulture);
            bool isKnownQuest = _questRegistry.TryGetQuest(quest.QuestId, out var knownQuest);

            if (ImGui.TableNextColumn())
            {
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(questId);
            }

            if (ImGui.TableNextColumn())
            {
                ImGui.AlignTextToFramePadding();
                var (color, icon, tooltipText) = _uiUtils.GetQuestStyle(quest.QuestId);
                using (var _ = _pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
                {
                    if (isKnownQuest)
                        ImGui.TextColored(color, icon.ToIconString());
                    else
                        ImGui.TextColored(ImGuiColors.DalamudGrey, icon.ToIconString());
                }

                if (ImGui.IsItemHovered())
                {
                    using var tooltip = ImRaii.Tooltip();
                    if (tooltip)
                    {
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

                        if (!isKnownQuest)
                        {
                            ImGui.SameLine();
                            ImGui.TextUnformatted("NoQuestPath");
                        }

                        DrawQuestUnlocks(quest, 0);
                    }
                }
            }

            if (ImGui.TableNextColumn())
            {
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(quest.Name);
            }

            if (ImGui.TableNextColumn())
            {
                using var id = ImRaii.PushId(questId);

                bool copy = ImGuiComponents.IconButton(FontAwesomeIcon.Copy);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Copy as file name");
                if (copy)
                    CopyToClipboard(quest, true);
                else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    CopyToClipboard(quest, false);

                ImGui.SameLine();

                if (knownQuest != null &&
                    knownQuest.FindSequence(0)?.LastStep()?.InteractionType == EInteractionType.AcceptQuest &&
                    !_gameFunctions.IsQuestAccepted(quest.QuestId) &&
                    !_gameFunctions.IsQuestLocked(quest.QuestId) &&
                    (quest.IsRepeatable || !_gameFunctions.IsQuestAcceptedOrComplete(quest.QuestId)))
                {
                    ImGui.BeginDisabled(_questController.NextQuest != null || _questController.SimulatedQuest != null);

                    bool startNextQuest = ImGuiComponents.IconButton(FontAwesomeIcon.Play);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Start as next quest");
                    if (startNextQuest)
                    {
                        _questController.SetNextQuest(knownQuest);
                        _questController.ExecuteNextStep(true);
                    }

                    ImGui.SameLine();

                    bool setNextQuest = ImGuiComponents.IconButton(FontAwesomeIcon.AngleDoubleRight);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Set as next quest");
                    if (setNextQuest)
                        _questController.SetNextQuest(knownQuest);

                    ImGui.EndDisabled();
                }
            }
        }
    }

    private void CopyToClipboard(QuestInfo quest, bool suffix)
    {
        string fileName = $"{quest.QuestId}_{quest.SimplifiedName}{(suffix ? ".json" : "")}";
        ImGui.SetClipboardText(fileName);
        _chatGui.Print($"Copied '{fileName}' to clipboard");
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
