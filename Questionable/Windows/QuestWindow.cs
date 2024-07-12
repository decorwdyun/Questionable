using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
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
using Questionable.Data;
using Questionable.External;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Windows;

internal sealed class QuestWindow : LWindow, IPersistableWindowConfig
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly MovementController _movementController;
    private readonly QuestController _questController;
    private readonly GameFunctions _gameFunctions;
    private readonly ChatFunctions _chatFunctions;
    private readonly IClientState _clientState;
    private readonly IFramework _framework;
    private readonly ITargetManager _targetManager;
    private readonly GameUiController _gameUiController;
    private readonly Configuration _configuration;
    private readonly NavmeshIpc _navmeshIpc;
    private readonly QuestRegistry _questRegistry;
    private readonly QuestData _questData;
    private readonly TerritoryData _territoryData;
    private readonly ILogger<QuestWindow> _logger;

    public QuestWindow(IDalamudPluginInterface pluginInterface,
        MovementController movementController,
        QuestController questController,
        GameFunctions gameFunctions,
        ChatFunctions chatFunctions,
        IClientState clientState,
        IFramework framework,
        ITargetManager targetManager,
        GameUiController gameUiController,
        Configuration configuration,
        NavmeshIpc navmeshIpc,
        QuestRegistry questRegistry,
        QuestData questData,
        TerritoryData territoryData,
        ILogger<QuestWindow> logger)
        : base("Questionable###Questionable", ImGuiWindowFlags.AlwaysAutoResize)
    {
        _pluginInterface = pluginInterface;
        _movementController = movementController;
        _questController = questController;
        _gameFunctions = gameFunctions;
        _chatFunctions = chatFunctions;
        _clientState = clientState;
        _framework = framework;
        _targetManager = targetManager;
        _gameUiController = gameUiController;
        _configuration = configuration;
        _navmeshIpc = navmeshIpc;
        _questRegistry = questRegistry;
        _questData = questData;
        _territoryData = territoryData;
        _logger = logger;

#if DEBUG
        IsOpen = true;
#endif
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(200, 30),
            MaximumSize = default
        };
        RespectCloseHotkey = false;
    }

    public WindowConfig WindowConfig => _configuration.DebugWindowConfig;

    public void SaveWindowConfig() => _pluginInterface.SavePluginConfig(_configuration);

    public override bool DrawConditions()
    {
        if (!_clientState.IsLoggedIn || _clientState.LocalPlayer == null)
            return false;

        if (_configuration.General.HideInAllInstances && _territoryData.IsDutyInstance(_clientState.TerritoryType))
            return false;

        var currentQuest = _questController.CurrentQuest;
        return currentQuest == null || !currentQuest.Quest.Root.TerritoryBlacklist.Contains(_clientState.TerritoryType);
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

    private void DrawQuest()
    {
        var currentQuestDetails = _questController.CurrentQuestDetails;
        QuestController.QuestProgress? currentQuest = currentQuestDetails?.Progress;
        QuestController.CurrentQuestType? currentQuestType = currentQuestDetails?.Type;
        if (currentQuest != null)
        {
            if (currentQuestType == QuestController.CurrentQuestType.Simulated)
            {
                var simulatedQuest = _questController.SimulatedQuest ?? currentQuest;
                using var _ = ImRaii.PushColor(ImGuiCol.Text, 0xFF0000FF);
                ImGui.TextUnformatted(
                    $"Simulated Quest: {simulatedQuest.Quest.Info.Name} / {simulatedQuest.Sequence} / {simulatedQuest.Step}");
            }
            else if (currentQuestType == QuestController.CurrentQuestType.Next)
            {
                var startedQuest = _questController.StartedQuest;
                if (startedQuest != null)
                {
                    ImGui.TextUnformatted(
                        $"Quest: {startedQuest.Quest.Info.Name} / {startedQuest.Sequence} / {startedQuest.Step}");
                }

                using var _ = ImRaii.PushColor(ImGuiCol.Text, 0xFF00FFFF);
                ImGui.TextUnformatted(
                    $"Next Quest: {currentQuest.Quest.Info.Name} / {currentQuest.Sequence} / {currentQuest.Step}");
            }
            else
            {
                ImGui.TextUnformatted(
                    $"Quest: {currentQuest.Quest.Info.Name} / {currentQuest.Sequence} / {currentQuest.Step}");
            }


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
            {
                if (currentQuest.Quest.QuestId == _questController.NextQuest?.Quest.QuestId)
                    ImGui.TextUnformatted("(Next quest in story line not accepted)");
                else
                    ImGui.TextUnformatted("(Not accepted)");
            }

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
                // if we haven't accepted this quest, mark it as next quest so that we can optionally use aetherytes to travel
                if (questWork == null)
                    _questController.SetNextQuest(currentQuest.Quest);

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
            bool lastStep = currentStep ==
                            currentQuest.Quest.FindSequence(currentQuest.Sequence)?.Steps.LastOrDefault();
            bool colored = currentStep != null
                           && !lastStep
                           && currentStep.InteractionType == EInteractionType.Instruction
                           && _questController.HasCurrentTaskMatching<WaitAtEnd.WaitNextStepOrSequence>();

            ImGui.BeginDisabled(lastStep);
            if (colored)
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.HealerGreen);
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.ArrowCircleRight, "Skip"))
            {
                _movementController.Stop();
                _questController.Skip(currentQuest.Quest.QuestId, currentQuest.Sequence);
            }

            if (colored)
                ImGui.PopStyleColor();
            ImGui.EndDisabled();

            bool autoAcceptNextQuest = _configuration.General.AutoAcceptNextQuest;
            if (ImGui.Checkbox("Automatically accept next quest", ref autoAcceptNextQuest))
            {
                _configuration.General.AutoAcceptNextQuest = autoAcceptNextQuest;
                _pluginInterface.SavePluginConfig(_configuration);
            }


            if (_questController.SimulatedQuest != null)
            {
                var simulatedQuest = _questController.SimulatedQuest;

                ImGui.Separator();
                ImGui.TextColored(ImGuiColors.DalamudRed, "Quest sim active (experimental)");
                ImGui.Text($"Sequence: {simulatedQuest.Sequence}");

                ImGui.BeginDisabled(simulatedQuest.Sequence == 0);
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Minus))
                {
                    _movementController.Stop();
                    _questController.Stop("Sim-");

                    byte oldSequence = simulatedQuest.Sequence;
                    byte newSequence = simulatedQuest.Quest.Root.QuestSequence
                        .Select(x => (byte)x.Sequence)
                        .LastOrDefault(x => x < oldSequence, byte.MinValue);

                    _questController.SimulatedQuest.SetSequence(newSequence);
                }

                ImGui.EndDisabled();

                ImGui.SameLine();
                ImGui.BeginDisabled(simulatedQuest.Sequence >= 255);
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus))
                {
                    _movementController.Stop();
                    _questController.Stop("Sim+");

                    byte oldSequence = simulatedQuest.Sequence;
                    byte newSequence = simulatedQuest.Quest.Root.QuestSequence
                        .Select(x => (byte)x.Sequence)
                        .FirstOrDefault(x => x > oldSequence, byte.MaxValue);

                    simulatedQuest.SetSequence(newSequence);
                }

                ImGui.EndDisabled();

                var simulatedSequence = simulatedQuest.Quest.FindSequence(simulatedQuest.Sequence);
                if (simulatedSequence != null)
                {
                    using var _ = ImRaii.PushId("SimulatedStep");

                    ImGui.Text($"Step: {simulatedQuest.Step} / {simulatedSequence.Steps.Count - 1}");

                    ImGui.BeginDisabled(simulatedQuest.Step == 0);
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Minus))
                    {
                        _movementController.Stop();
                        _questController.Stop("SimStep-");

                        simulatedQuest.SetStep(Math.Min(simulatedQuest.Step - 1,
                            simulatedSequence.Steps.Count - 1));
                    }

                    ImGui.EndDisabled();

                    ImGui.SameLine();
                    ImGui.BeginDisabled(simulatedQuest.Step >= simulatedSequence.Steps.Count);
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus))
                    {
                        _movementController.Stop();
                        _questController.Stop("SimStep+");

                        simulatedQuest.SetStep(
                            simulatedQuest.Step == simulatedSequence.Steps.Count - 1
                                ? 255
                                : (simulatedQuest.Step + 1));
                    }

                    ImGui.EndDisabled();

                    if (ImGui.Button("Skip current task"))
                    {
                        _questController.SkipSimulatedTask();
                    }

                    ImGui.SameLine();
                    if (ImGui.Button("Clear sim"))
                    {
                        _questController.SimulateQuest(null);

                        _movementController.Stop();
                        _questController.Stop("ClearSim");
                    }
                }
            }
        }
        else
            ImGui.Text("No active quest");
    }

    private unsafe void DrawCreationUtils()
    {
        Debug.Assert(_clientState.LocalPlayer != null, "_clientState.LocalPlayer != null");

        string territoryName = _territoryData.GetNameAndId(_clientState.TerritoryType);
        ImGui.Text(territoryName);

        if (_gameFunctions.IsFlyingUnlockedInCurrentZone())
        {
            ImGui.SameLine();
            ImGui.Text(SeIconChar.BotanistSprout.ToIconString());
        }

        var q = _gameFunctions.GetCurrentQuest();
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
                        _questRegistry.TryGetQuest(questManager->NormalQuests[trackedQuest.Index].QuestId,
                            out var quest);
                        ImGui.Text(
                            $"Tracked quest: {questManager->NormalQuests[trackedQuest.Index].QuestId}, {trackedQuest.Index}: {quest?.Name}");
                        break;
                }
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
            ImGui.BeginDisabled(!_questData.IsIssuerOfAnyQuest(_targetManager.Target.DataId));
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Bars))
                _questData.ShowQuestsIssuedByTarget();
            ImGui.EndDisabled();

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.MousePointer))
            {
                ulong result = TargetSystem.Instance()->InteractWithObject(
                    (GameObject*)_targetManager.Target.Address, false);
                _logger.LogInformation("XXXXX Interaction Result: {Result}", result);
            }

            ImGui.SameLine();

            ImGuiComponents.IconButton(FontAwesomeIcon.Copy);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
                string interactionType = gameObject->NamePlateIconId switch
                {
                    71201 or 71211 or 71221 or 71231 or 71341 or 71351 => "AcceptQuest",
                    71202 or 71212 or 71222 or 71232 or 71342 or 71352 => "AcceptQuest", // repeatable
                    71205 or 71215 or 71225 or 71235 or 71345 or 71355 => "CompleteQuest",
                    _ => "Interact",
                };
                ImGui.SetClipboardText($$"""
                                         "DataId": {{_targetManager.Target.DataId}},
                                         "Position": {
                                             "X": {{_targetManager.Target.Position.X.ToString(CultureInfo.InvariantCulture)}},
                                             "Y": {{_targetManager.Target.Position.Y.ToString(CultureInfo.InvariantCulture)}},
                                             "Z": {{_targetManager.Target.Position.Z.ToString(CultureInfo.InvariantCulture)}}
                                         },
                                         "TerritoryId": {{_clientState.TerritoryType}},
                                         "InteractionType": "{{interactionType}}"
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
            ImGuiComponents.IconButton(FontAwesomeIcon.Copy);
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
                            map->FlagMapMarker.TerritoryId != _clientState.TerritoryType ||
                            !_navmeshIpc.IsReady);
        if (ImGui.Button("Move to Flag"))
        {
            _movementController.Destination = null;
            _chatFunctions.ExecuteCommand(
                $"/vnav {(_gameFunctions.IsFlyingUnlockedInCurrentZone() ? "flyflag" : "moveflag")}");
        }

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
