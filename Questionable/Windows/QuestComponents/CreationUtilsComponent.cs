using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using ImGuiNET;
using Microsoft.Extensions.Logging;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Common;
using Questionable.Model.Questing;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace Questionable.Windows.QuestComponents;

internal sealed class CreationUtilsComponent
{
    private readonly MovementController _movementController;
    private readonly GameFunctions _gameFunctions;
    private readonly QuestFunctions _questFunctions;
    private readonly TerritoryData _territoryData;
    private readonly QuestData _questData;
    private readonly QuestSelectionWindow _questSelectionWindow;
    private readonly IClientState _clientState;
    private readonly ITargetManager _targetManager;
    private readonly ICondition _condition;
    private readonly IGameGui _gameGui;
    private readonly ILogger<CreationUtilsComponent> _logger;

    public CreationUtilsComponent(
        MovementController movementController,
        GameFunctions gameFunctions,
        QuestFunctions questFunctions,
        TerritoryData territoryData,
        QuestData questData,
        QuestSelectionWindow questSelectionWindow,
        IClientState clientState,
        ITargetManager targetManager,
        ICondition condition,
        IGameGui gameGui,
        ILogger<CreationUtilsComponent> logger)
    {
        _movementController = movementController;
        _gameFunctions = gameFunctions;
        _questFunctions = questFunctions;
        _territoryData = territoryData;
        _questData = questData;
        _questSelectionWindow = questSelectionWindow;
        _clientState = clientState;
        _targetManager = targetManager;
        _condition = condition;
        _gameGui = gameGui;
        _logger = logger;
    }

    public unsafe void Draw()
    {
        Debug.Assert(_clientState.LocalPlayer != null, "_clientState.LocalPlayer != null");

        string territoryName = _territoryData.GetNameAndId(_clientState.TerritoryType);
        ImGui.Text(territoryName);

        if (_gameFunctions.IsFlyingUnlockedInCurrentZone())
        {
            ImGui.SameLine();
            ImGui.Text(SeIconChar.BotanistSprout.ToIconString());
        }

        var q = _questFunctions.GetCurrentQuest();
        ImGui.Text($"Current Quest: {q.CurrentQuest} → {q.Sequence}");

#if false
        var questManager = QuestManager.Instance();
        if (questManager != null)
        {
            for (int i = questManager->TrackedQuests.Length - 1; i >= 0; --i)
            {
                var trackedQuest = questManager->TrackedQuests[i];
                switch (trackedQuest.QuestType)
                {
                    default:
                        ImGui.Text($"Tracked quest {i}: {trackedQuest.QuestType}, {trackedQuest.Index}");
                        break;

                    case 1:
                        //_questRegistry.TryGetQuest(questManager->NormalQuests[trackedQuest.Index].QuestId,
                        //    out var quest);
                        ImGui.Text(
                            $"Quest: {questManager->NormalQuests[trackedQuest.Index].QuestId}, {trackedQuest.Index}");
                        break;

                    case 2:
                        ImGui.Text($"Leve: {questManager->LeveQuests[trackedQuest.Index].LeveId}, {trackedQuest.Index}");
                        break;
                }
            }
        }
#endif

#if false
        var director = UIState.Instance()->DirectorTodo.Director;
        if (director != null)
        {
            ImGui.Text($"Director: {director->ContentId}");
            ImGui.Text($"Seq: {director->Sequence}");
            ImGui.Text($"Ico: {director->IconId}");
            if (director->EventHandlerInfo != null)
            {
                ImGui.Text($"  EHI CI: {director->EventHandlerInfo->EventId.ContentId}");
                ImGui.Text($"  EHI EI: {director->EventHandlerInfo->EventId.Id}");
                ImGui.Text($"  EHI EEI: {director->EventHandlerInfo->EventId.EntryId}");
                ImGui.Text($"  EHI F: {director->EventHandlerInfo->Flags}");
            }
        }
#endif

        if (_targetManager.Target != null)
        {
            ImGui.Separator();
            ImGui.Text(string.Create(CultureInfo.InvariantCulture,
                $"Target: {_targetManager.Target.Name}  ({_targetManager.Target.ObjectKind}; {_targetManager.Target.DataId})"));

            GameObject* gameObject = (GameObject*)_targetManager.Target.Address;
            ImGui.Text(string.Create(CultureInfo.InvariantCulture,
                $"Distance: {(_targetManager.Target.Position - _clientState.LocalPlayer.Position).Length():F2}"));
            ImGui.SameLine();

            float verticalDistance = _targetManager.Target.Position.Y - _clientState.LocalPlayer.Position.Y;
            string verticalDistanceText = string.Create(CultureInfo.InvariantCulture, $"Y: {verticalDistance:F2}");
            if (Math.Abs(verticalDistance) >= MovementController.DefaultVerticalInteractionDistance)
                ImGui.TextColored(ImGuiColors.DalamudOrange, verticalDistanceText);
            else
                ImGui.Text(verticalDistanceText);

            ImGui.SameLine();
            ImGui.Text($"QM: {gameObject->NamePlateIconId}");

            ImGui.BeginDisabled(!_movementController.IsNavmeshReady || _gameFunctions.IsOccupied());
            if (!_movementController.IsPathfinding)
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Bullseye, "To Target"))
                {
                    _movementController.NavigateTo(EMovementType.DebugWindow, _targetManager.Target.DataId,
                        _targetManager.Target.Position,
                        fly: _condition[ConditionFlag.Mounted] && _gameFunctions.IsFlyingUnlockedInCurrentZone(),
                        sprint: true);
                }
            }
            else
            {
                if (ImGui.Button("Cancel pathfinding"))
                    _movementController.ResetPathfinding();
            }

            ImGui.EndDisabled();

            ImGui.SameLine();
            ImGui.BeginDisabled(!_questData.IsIssuerOfAnyQuest(_targetManager.Target.DataId));
            bool showQuests = ImGuiComponents.IconButton(FontAwesomeIcon.MapMarkerAlt);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Show all Quests starting with your current target.");
            if (showQuests)
                _questSelectionWindow.OpenForTarget(_targetManager.Target);

            ImGui.EndDisabled();

            ImGui.BeginDisabled(_gameFunctions.IsOccupied());
            ImGui.SameLine();
            bool interact = ImGuiComponents.IconButton(FontAwesomeIcon.MousePointer);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Interact with your current target.");
            if (interact)
            {
                ulong result = TargetSystem.Instance()->InteractWithObject(
                    (GameObject*)_targetManager.Target.Address, false);
                _logger.LogInformation("XXXXX Interaction Result: {Result}", result);
            }
            ImGui.EndDisabled();
            ImGui.SameLine();

            bool copy = ImGuiComponents.IconButton(FontAwesomeIcon.Copy);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(
                    "Left click: Copy target position as JSON.\nRight click: Copy target position as C# code.");
            if (copy)
            {
                var target = _targetManager.Target;
                if (target.ObjectKind == ObjectKind.GatheringPoint)
                {
                    ImGui.SetClipboardText($$"""
                                             "DataId": {{target.DataId}},
                                             "Position": {
                                                 "X": {{target.Position.X.ToString(CultureInfo.InvariantCulture)}},
                                                 "Y": {{target.Position.Y.ToString(CultureInfo.InvariantCulture)}},
                                                 "Z": {{target.Position.Z.ToString(CultureInfo.InvariantCulture)}}
                                             }
                                             """);
                }
                else
                {
                    string interactionType = gameObject->NamePlateIconId switch
                    {
                        71201 or 71211 or 71221 or 71231 or 71341 or 71351 => "AcceptQuest",
                        71202 or 71212 or 71222 or 71232 or 71342 or 71352 => "AcceptQuest", // repeatable
                        71205 or 71215 or 71225 or 71235 or 71345 or 71355 => "CompleteQuest",
                        _ => "Interact",
                    };
                    ImGui.SetClipboardText($$"""
                                             "DataId": {{target.DataId}},
                                             "Position": {
                                                 "X": {{target.Position.X.ToString(CultureInfo.InvariantCulture)}},
                                                 "Y": {{target.Position.Y.ToString(CultureInfo.InvariantCulture)}},
                                                 "Z": {{target.Position.Z.ToString(CultureInfo.InvariantCulture)}}
                                             },
                                             "TerritoryId": {{_clientState.TerritoryType}},
                                             "InteractionType": "{{interactionType}}"
                                             """);
                }
            }
            else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                if (_targetManager.Target.ObjectKind == ObjectKind.Aetheryte)
                {
                    EAetheryteLocation location = (EAetheryteLocation)_targetManager.Target.DataId;
                    ImGui.SetClipboardText(string.Create(CultureInfo.InvariantCulture,
                        $"{{EAetheryteLocation.{location}, new({_targetManager.Target.Position.X}f, {_targetManager.Target.Position.Y}f, {_targetManager.Target.Position.Z}f)}},"));
                }
                else
                    ImGui.SetClipboardText(string.Create(CultureInfo.InvariantCulture,
                        $"new({_targetManager.Target.Position.X}f, {_targetManager.Target.Position.Y}f, {_targetManager.Target.Position.Z}f)"));
            }
        }
        else
        {
            bool copy = ImGuiComponents.IconButton(FontAwesomeIcon.Copy);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(
                    "Left click: Copy your position as JSON.\nRight click: Copy your position as C# code.");
            if (copy)
            {
                ImGui.SetClipboardText($$"""
                                         "Position": {
                                             "X": {{_clientState.LocalPlayer.Position.X.ToString(CultureInfo.InvariantCulture)}},
                                             "Y": {{_clientState.LocalPlayer.Position.Y.ToString(CultureInfo.InvariantCulture)}},
                                             "Z": {{_clientState.LocalPlayer.Position.Z.ToString(CultureInfo.InvariantCulture)}}
                                         },
                                         "TerritoryId": {{_clientState.TerritoryType}},
                                         "InteractionType": ""
                                         """);
            }
            else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                Vector3 position = _clientState.LocalPlayer!.Position;
                ImGui.SetClipboardText(string.Create(CultureInfo.InvariantCulture,
                    $"new({position.X}f, {position.Y}f, {position.Z}f)"));
            }
        }

        ulong hoveredItemId = _gameGui.HoveredItem;
        if (hoveredItemId != 0)
        {
            ImGui.Separator();
            ImGui.Text($"Hovered Item: {hoveredItemId}");
        }
    }
}
