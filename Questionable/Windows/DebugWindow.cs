using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using LLib.ImGui;
using Microsoft.Extensions.Logging;
using Questionable.Controller;
using Questionable.Controller.Steps.BaseFactory;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Windows;

internal sealed class DebugWindow : LWindow, IPersistableWindowConfig
{
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly MovementController _movementController;
    private readonly QuestController _questController;
    private readonly GameFunctions _gameFunctions;
    private readonly IClientState _clientState;
    private readonly IFramework _framework;
    private readonly ITargetManager _targetManager;
    private readonly GameUiController _gameUiController;
    private readonly Configuration _configuration;
    private readonly ILogger<DebugWindow> _logger;

    public DebugWindow(DalamudPluginInterface pluginInterface,
        MovementController movementController,
        QuestController questController,
        GameFunctions gameFunctions,
        IClientState clientState,
        IFramework framework,
        ITargetManager targetManager,
        GameUiController gameUiController,
        Configuration configuration,
        ILogger<DebugWindow> logger)
        : base("Questionable", ImGuiWindowFlags.AlwaysAutoResize)
    {
        _pluginInterface = pluginInterface;
        _movementController = movementController;
        _questController = questController;
        _gameFunctions = gameFunctions;
        _clientState = clientState;
        _framework = framework;
        _targetManager = targetManager;
        _gameUiController = gameUiController;
        _configuration = configuration;
        _logger = logger;

        IsOpen = true;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(200, 30),
            MaximumSize = default
        };
    }

    public WindowConfig WindowConfig => _configuration.DebugWindowConfig;

    public void SaveWindowConfig() => _pluginInterface.SavePluginConfig(_configuration);

    public override bool DrawConditions()
    {
        if (!_clientState.IsLoggedIn || _clientState.LocalPlayer == null)
            return false;

        var currentQuest = _questController.CurrentQuest;
        return currentQuest == null || !currentQuest.Quest.Data.TerritoryBlacklist.Contains(_clientState.TerritoryType);
    }

    public override void Draw()
    {
        DrawQuest();
        ImGui.Separator();

        DrawCreationUtils();
        ImGui.Separator();

        DrawQuickAccessButtons();
        DrawRemainingTasks();
    }

    private unsafe void DrawQuest()
    {
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
                {
                    vars += qw.Variables[i] + " ";
                    if (i % 2 == 1)
                        vars += "   ";
                }

                // For combat quests, a sequence to kill 3 enemies works a bit like this:
                // Trigger enemies → 0
                // Kill first enemy → 1
                // Kill second enemy → 2
                // Last enemy → increase sequence, reset variable to 0
                // The order in which enemies are killed doesn't seem to matter.
                // If multiple waves spawn, this continues to count up (e.g. 1 enemy from wave 1, 2 enemies from wave 2, 1 from wave 3) would count to 3 then 0
                ImGui.Text($"QW: {vars.Trim()}");
            }
            else
                ImGui.TextUnformatted("(Not accepted)");

            ImGui.TextUnformatted(_questController.DebugState ?? "--");
            ImGui.EndDisabled();
            ImGui.TextUnformatted(_questController.Comment ?? "--");

            //var nextStep = _questController.GetNextStep();
            //ImGui.BeginDisabled(nextStep.Step == null);
            ImGui.Text(_questController.ToStatString());
            //ImGui.EndDisabled();

            ImGui.BeginDisabled(_questController.IsRunning);
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Play))
            {
                _questController.ExecuteNextStep(true);
            }

            ImGui.SameLine();

            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.StepForward, "Step"))
            {
                _questController.ExecuteNextStep(false);
            }

            ImGui.EndDisabled();
            ImGui.SameLine();

            if (ImGuiComponents.IconButton(FontAwesomeIcon.Stop))
            {
                _movementController.Stop();
                _questController.Stop("Manual");
            }

            QuestStep? currentStep = currentQuest.Quest
                .FindSequence(currentQuest.Sequence)
                ?.FindStep(currentQuest.Step);
            bool colored = currentStep != null && currentStep.InteractionType == EInteractionType.Instruction
                                               && _questController
                                                   .HasCurrentTaskMatching<WaitAtEnd.WaitNextStepOrSequence>();

            if (colored)
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.HealerGreen);
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.ArrowCircleRight, "Skip"))
            {
                _movementController.Stop();
                _questController.Stop("Manual");
                _questController.IncreaseStepCount();
            }

            if (colored)
                ImGui.PopStyleColor();

            bool autoAcceptNextQuest = _configuration.General.AutoAcceptNextQuest;
            if (ImGui.Checkbox("Automatically accept next quest", ref autoAcceptNextQuest))
            {
                _configuration.General.AutoAcceptNextQuest = autoAcceptNextQuest;
                _pluginInterface.SavePluginConfig(_configuration);
            }
        }
        else
            ImGui.Text("No active quest");
    }

    private unsafe void DrawCreationUtils()
    {
        Debug.Assert(_clientState.LocalPlayer != null, "_clientState.LocalPlayer != null");

        ImGui.Text(
            $"Current TerritoryId: {_clientState.TerritoryType}, Flying: {(_gameFunctions.IsFlyingUnlockedInCurrentZone() ? "Yes" : "No")}");

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

            GameObject* gameObject = (GameObject*)_targetManager.Target.Address;
            ImGui.Text(string.Create(CultureInfo.InvariantCulture,
                $"Distance: {(_targetManager.Target.Position - _clientState.LocalPlayer.Position).Length():F2}, Y: {_targetManager.Target.Position.Y - _clientState.LocalPlayer.Position.Y:F2} | QM: {gameObject->NamePlateIconId}"));

            ImGui.BeginDisabled(!_movementController.IsNavmeshReady);
            if (!_movementController.IsPathfinding)
            {
                if (ImGui.Button("Move to Target"))
                {
                    _movementController.NavigateTo(EMovementType.DebugWindow, _targetManager.Target.DataId,
                        _targetManager.Target.Position, _gameFunctions.IsFlyingUnlockedInCurrentZone(),
                        true);
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
                ulong result = TargetSystem.Instance()->InteractWithObject(
                    (GameObject*)_targetManager.Target.Address, false);
                _logger.LogInformation("XXXXX Interaction Result: {Result}", result);
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
            ImGui.Button($"Copy");
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
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
    }

    private unsafe void DrawQuickAccessButtons()
    {
        var map = AgentMap.Instance();
        ImGui.BeginDisabled(map == null || map->IsFlagMarkerSet == 0 ||
                            map->FlagMapMarker.TerritoryId != _clientState.TerritoryType);
        if (ImGui.Button("Move to Flag"))
            _gameFunctions.ExecuteCommand(
                $"/vnav {(_gameFunctions.IsFlyingUnlockedInCurrentZone() ? "flyflag" : "moveflag")}");
        ImGui.EndDisabled();

        ImGui.SameLine();

        ImGui.BeginDisabled(!_movementController.IsPathRunning);
        if (ImGui.Button("Stop Nav"))
        {
            _movementController.Stop();
            _questController.Stop("Manual");
        }

        ImGui.EndDisabled();

        if (ImGui.Button("Reload Data"))
        {
            _questController.Reload();
            _framework.RunOnTick(() => _gameUiController.HandleCurrentDialogueChoices(),
                TimeSpan.FromMilliseconds(200));
        }
    }

    private void DrawRemainingTasks()
    {
        var remainingTasks = _questController.GetRemainingTaskNames();
        if (remainingTasks.Count > 0)
        {
            ImGui.Separator();
            ImGui.BeginDisabled();
            foreach (var task in remainingTasks)
                ImGui.TextUnformatted(task);
            ImGui.EndDisabled();
        }
    }
}
