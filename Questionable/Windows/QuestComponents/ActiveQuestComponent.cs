using System;
using System.Globalization;
using System.Linq;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using ImGuiNET;
using Questionable.Controller;
using Questionable.Controller.Steps.Shared;
using Questionable.Model.V1;

namespace Questionable.Windows.QuestComponents;

internal sealed class ActiveQuestComponent
{
    private readonly QuestController _questController;
    private readonly MovementController _movementController;
    private readonly CombatController _combatController;
    private readonly GameFunctions _gameFunctions;
    private readonly ICommandManager _commandManager;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly Configuration _configuration;
    private readonly QuestRegistry _questRegistry;

    public ActiveQuestComponent(QuestController questController, MovementController movementController,
        CombatController combatController, GameFunctions gameFunctions, ICommandManager commandManager,
        IDalamudPluginInterface pluginInterface, Configuration configuration, QuestRegistry questRegistry)
    {
        _questController = questController;
        _movementController = movementController;
        _combatController = combatController;
        _gameFunctions = gameFunctions;
        _commandManager = commandManager;
        _pluginInterface = pluginInterface;
        _configuration = configuration;
        _questRegistry = questRegistry;
    }

    public void Draw()
    {
        var currentQuestDetails = _questController.CurrentQuestDetails;
        QuestController.QuestProgress? currentQuest = currentQuestDetails?.Progress;
        QuestController.CurrentQuestType? currentQuestType = currentQuestDetails?.Type;
        if (currentQuest != null)
        {
            DrawQuestNames(currentQuest, currentQuestType);
            var questWork = DrawQuestWork(currentQuest);

            if (_combatController.IsRunning)
                ImGui.TextColored(ImGuiColors.DalamudOrange, "In Combat");
            else
            {
                ImGui.BeginDisabled();
                ImGui.TextUnformatted(_questController.DebugState ?? string.Empty);
                ImGui.EndDisabled();
            }

            QuestSequence? currentSequence = currentQuest.Quest.FindSequence(currentQuest.Sequence);
            QuestStep? currentStep = currentSequence?.FindStep(currentQuest.Step);
            bool colored = currentStep is
                { InteractionType: EInteractionType.Instruction or EInteractionType.WaitForManualProgress };
            if (colored)
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudOrange);
            ImGui.TextUnformatted(currentStep?.Comment ??
                                  currentSequence?.Comment ?? currentQuest.Quest.Root.Comment ?? string.Empty);
            if (colored)
                ImGui.PopStyleColor();

            //var nextStep = _questController.GetNextStep();
            //ImGui.BeginDisabled(nextStep.Step == null);
            ImGui.Text(_questController.ToStatString());
            //ImGui.EndDisabled();

            DrawQuestIcons(currentQuest, currentStep, questWork);

            DrawSimulationControls();
        }
        else
        {
            ImGui.Text("No active quest");
            ImGui.TextColored(ImGuiColors.DalamudGrey, $"{_questRegistry.Count} quests loaded");
        }
    }

    private void DrawQuestNames(QuestController.QuestProgress currentQuest,
        QuestController.CurrentQuestType? currentQuestType)
    {
        if (currentQuestType == QuestController.CurrentQuestType.Simulated)
        {
            var simulatedQuest = _questController.SimulatedQuest ?? currentQuest;
            using var _ = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
            ImGui.TextUnformatted(
                $"Simulated Quest: {simulatedQuest.Quest.Info.Name} / {simulatedQuest.Sequence} / {simulatedQuest.Step}");
        }
        else if (currentQuestType == QuestController.CurrentQuestType.Next)
        {
            var startedQuest = _questController.StartedQuest;
            if (startedQuest != null)
                DrawCurrentQuest(startedQuest);

            using var _ = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudYellow);
            ImGui.TextUnformatted(
                $"Next Quest: {currentQuest.Quest.Info.Name} / {currentQuest.Sequence} / {currentQuest.Step}");
        }
        else
            DrawCurrentQuest(currentQuest);
    }

    private void DrawCurrentQuest(QuestController.QuestProgress currentQuest)
    {
        ImGui.TextUnformatted(
            $"Quest: {currentQuest.Quest.Info.Name} / {currentQuest.Sequence} / {currentQuest.Step}");

        if (currentQuest.Quest.Root.Disabled)
        {
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudRed, "Disabled");
        }

        if (_configuration.Advanced.AdditionalStatusInformation && _questController.IsInterruptible())
        {
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudYellow, "Interruptible");
        }
    }

    private QuestWork? DrawQuestWork(QuestController.QuestProgress currentQuest)
    {
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

        ImGui.EndDisabled();
        return questWork;
    }

    private void DrawQuestIcons(QuestController.QuestProgress currentQuest, QuestStep? currentStep,
        QuestWork? questWork)
    {
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

        bool lastStep = currentStep ==
                        currentQuest.Quest.FindSequence(currentQuest.Sequence)?.Steps.LastOrDefault();
        bool colored = currentStep != null
                       && !lastStep
                       && currentStep.InteractionType == EInteractionType.Instruction
                       && _questController.HasCurrentTaskMatching<WaitAtEnd.WaitNextStepOrSequence>();

        ImGui.BeginDisabled(lastStep);
        if (colored)
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.ParsedGreen);
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.ArrowCircleRight, "Skip"))
        {
            _movementController.Stop();
            _questController.Skip(currentQuest.Quest.QuestId, currentQuest.Sequence);
        }

        if (colored)
            ImGui.PopStyleColor();
        ImGui.EndDisabled();

        if (_commandManager.Commands.TryGetValue("/questinfo", out var commandInfo))
        {
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Atlas))
                _commandManager.DispatchCommand("/questinfo",
                    currentQuest.Quest.QuestId.ToString(CultureInfo.InvariantCulture), commandInfo);
        }

        bool autoAcceptNextQuest = _configuration.General.AutoAcceptNextQuest;
        if (ImGui.Checkbox("Automatically accept next quest", ref autoAcceptNextQuest))
        {
            _configuration.General.AutoAcceptNextQuest = autoAcceptNextQuest;
            _pluginInterface.SavePluginConfig(_configuration);
        }
    }

    private void DrawSimulationControls()
    {
        if (_questController.SimulatedQuest == null)
            return;

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
