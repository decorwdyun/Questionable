using System.Globalization;
using System.Numerics;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Questionable.Controller;
using Questionable.Model.V1;

namespace Questionable.Windows;

internal sealed class DebugWindow : Window
{
    private readonly MovementController _movementController;
    private readonly QuestController _questController;
    private readonly GameFunctions _gameFunctions;
    private readonly IClientState _clientState;
    private readonly ITargetManager _targetManager;

    public DebugWindow(MovementController movementController, QuestController questController,
        GameFunctions gameFunctions, IClientState clientState,
        ITargetManager targetManager)
        : base("Questionable", ImGuiWindowFlags.AlwaysAutoResize)
    {
        _movementController = movementController;
        _questController = questController;
        _gameFunctions = gameFunctions;
        _clientState = clientState;
        _targetManager = targetManager;

        IsOpen = true;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(100, 0),
            MaximumSize = default
        };
    }

    public override unsafe void Draw()
    {
        if (!_clientState.IsLoggedIn || _clientState.LocalPlayer == null)
            return;

        var currentQuest = _questController.CurrentQuest;
        if (currentQuest != null)
        {
            ImGui.TextUnformatted($"Quest: {currentQuest.Quest.Name} / {currentQuest.Sequence} / {currentQuest.Step}");
            ImGui.TextUnformatted(_questController.DebugState ?? "--");

            ImGui.BeginDisabled(_questController.GetNextStep().Step == null);
            ImGui.Text($"{_questController.GetNextStep().Step?.Position}");
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Play))
            {
                _questController.ExecuteNextStep();
            }

            ImGui.SameLine();

            if (ImGuiComponents.IconButton(FontAwesomeIcon.StepForward))
            {
                _questController.IncreaseStepCount();
            }

            ImGui.EndDisabled();
        }
        else
            ImGui.Text("No active quest");

        ImGui.Separator();

        ImGui.Text(
            $"Current TerritoryId: {_clientState.TerritoryType}, Flying: {(_gameFunctions.IsFlyingUnlocked(_clientState.TerritoryType) ? "Yes" : "No")}");

        var q = _gameFunctions.GetCurrentQuest();
        ImGui.Text($"Current Quest: {q.CurrentQuest} → {q.Sequence}");

        if (_targetManager.Target != null)
        {
            ImGui.Separator();
            ImGui.Text(string.Create(CultureInfo.InvariantCulture,
                $"Target: {_targetManager.Target.Name} ({(_targetManager.Target.Position - _clientState.LocalPlayer.Position).Length():F2})"));

            ImGui.BeginDisabled(!_movementController.IsNavmeshReady);
            if (!_movementController.IsPathfinding)
            {
                if (ImGui.Button("Move to Target"))
                {
                    _movementController.NavigateTo(EMovementType.DebugWindow, _targetManager.Target.Position,
                        _gameFunctions.IsFlyingUnlocked(_clientState.TerritoryType));
                }
            }
            else
            {
                if (ImGui.Button("Cancel pathfinding"))
                    _movementController.ResetPathfinding();
            }

            ImGui.EndDisabled();

            ImGui.SameLine();
            if (ImGui.Button("Interact"))
            {
                TargetSystem.Instance()->InteractWithObject(
                    (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)_targetManager.Target.Address, false);
            }

            ImGui.SameLine();

            ImGui.Button("Copy");
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
                ImGui.SetClipboardText($$"""
                                         "DataId": {{_targetManager.Target.DataId}},
                                         "Position": {
                                             "X": {{_targetManager.Target.Position.X.ToString(CultureInfo.InvariantCulture)}},
                                             "Y": {{_targetManager.Target.Position.Y.ToString(CultureInfo.InvariantCulture)}},
                                             "Z": {{_targetManager.Target.Position.Z.ToString(CultureInfo.InvariantCulture)}}
                                         },
                                         "TerritoryId": {{_clientState.TerritoryType}},
                                         "InteractionType": "Interact"
                                         """);
            }
            else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                EAetheryteLocation location = (EAetheryteLocation)_targetManager.Target.DataId;
                ImGui.SetClipboardText(string.Create(CultureInfo.InvariantCulture,
                    $"{{EAetheryteLocation.{location}, new({_targetManager.Target.Position.X}f, {_targetManager.Target.Position.Y}f, {_targetManager.Target.Position.Z}f)}},"));
            }
        }
        else
        {
            if (ImGui.Button($"Copy"))
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
        }

        ImGui.Separator();

        var map = AgentMap.Instance();
        ImGui.BeginDisabled(map == null || map->IsFlagMarkerSet == 0 ||
                            map->FlagMapMarker.TerritoryId != _clientState.TerritoryType);
        if (ImGui.Button("Move to Flag"))
            _gameFunctions.ExecuteCommand(
                $"/vnav {(_gameFunctions.IsFlyingUnlocked(_clientState.TerritoryType) ? "flyflag" : "moveflag")}");
        ImGui.EndDisabled();

        ImGui.SameLine();

        ImGui.BeginDisabled(!_movementController.IsPathRunning);
        if (ImGui.Button("Stop Nav"))
            _movementController.Stop();
        ImGui.EndDisabled();
    }
}
