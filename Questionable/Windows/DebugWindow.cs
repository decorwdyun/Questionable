using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
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
            MinimumSize = new Vector2(200, 30),
            MaximumSize = default
        };
    }

    public override unsafe void Draw()
    {
        if (!_clientState.IsLoggedIn || _clientState.LocalPlayer == null)
        {
            ImGui.Text("Not logged in.");
            return;
        }

        var currentQuest = _questController.CurrentQuest;
        if (currentQuest != null)
        {
            ImGui.TextUnformatted($"Quest: {currentQuest.Quest.Name} / {currentQuest.Sequence} / {currentQuest.Step}");

            ImGui.BeginDisabled();
            var questWork = _gameFunctions.GetQuestEx(currentQuest.Quest.QuestId);
            if (questWork != null)
            {
                var qw = questWork.Value;
                string vars = "";
                for (int i = 0; i < 6; ++i)
                    vars += qw.Variables[i] + " ";

                // For combat quests, a sequence to kill 3 enemies works a bit like this:
                // Trigger enemies → 0
                // Kill first enemy → 1
                // Kill second enemy → 2
                // Last enemy → increase sequence, reset variable to 0
                // The order in which enemies are killed doesn't seem to matter.
                // If multiple waves spawn, this continues to count up (e.g. 1 enemy from wave 1, 2 enemies from wave 2, 1 from wave 3) would count to 3 then 0
                ImGui.Text($"QW: {vars.Trim()} / {qw.Flags}");
            }
            else
                ImGui.TextUnformatted("(Not accepted)");

            ImGui.TextUnformatted(_questController.DebugState ?? "--");
            ImGui.EndDisabled();
            ImGui.TextUnformatted(_questController.Comment ?? "--");

            var nextStep = _questController.GetNextStep();
            ImGui.BeginDisabled(nextStep.Step == null);
            ImGui.Text(string.Create(CultureInfo.InvariantCulture,
                $"{nextStep.Step?.InteractionType} @ {nextStep.Step?.Position}"));
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

        var questManager = QuestManager.Instance();
        if (questManager != null)
        {
            // unsure how these are sorted
            for (int i = 0; i < 1 /*questManager->TrackedQuestsSpan.Length*/; ++i)
            {
                var trackedQuest = questManager->TrackedQuestsSpan[i];
                switch (trackedQuest.QuestType)
                {
                    default:
                        ImGui.Text($"Tracked quest {i}: {trackedQuest.QuestType}, {trackedQuest.Index}");
                        break;

                    case 1:
                        ImGui.Text(
                            $"Tracked quest: {questManager->NormalQuestsSpan[trackedQuest.Index].QuestId}, {trackedQuest.Index}");
                        break;
                }
            }
        }


        if (_targetManager.Target != null)
        {
            ImGui.Separator();
            ImGui.Text(string.Create(CultureInfo.InvariantCulture,
                $"Target: {_targetManager.Target.Name}  ({_targetManager.Target.ObjectKind}; {_targetManager.Target.DataId})"));
            ImGui.Text(string.Create(CultureInfo.InvariantCulture,
                $"Distance: {(_targetManager.Target.Position - _clientState.LocalPlayer.Position).Length():F2}, Y: {_targetManager.Target.Position.Y - _clientState.LocalPlayer.Position.Y:F2}"));

            ImGui.BeginDisabled(!_movementController.IsNavmeshReady);
            if (!_movementController.IsPathfinding)
            {
                if (ImGui.Button("Move to Target"))
                {
                    _movementController.NavigateTo(EMovementType.DebugWindow, _targetManager.Target.DataId,
                        _targetManager.Target.Position, _gameFunctions.IsFlyingUnlocked(_clientState.TerritoryType));
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

        if (ImGui.Button("Reload Data"))
            _questController.Reload();
    }
}
