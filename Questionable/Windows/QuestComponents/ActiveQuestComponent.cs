﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Questionable.Controller;
using Questionable.Controller.Steps.Shared;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Windows.QuestComponents;

internal sealed partial class ActiveQuestComponent
{
    [GeneratedRegex(@"\s\s+", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex MultipleWhitespaceRegex();

    private readonly QuestController _questController;
    private readonly MovementController _movementController;
    private readonly CombatController _combatController;
    private readonly GatheringController _gatheringController;
    private readonly QuestFunctions _questFunctions;
    private readonly ICommandManager _commandManager;
    private readonly Configuration _configuration;
    private readonly QuestRegistry _questRegistry;
    private readonly PriorityWindow _priorityWindow;
    private readonly IChatGui _chatGui;

    public ActiveQuestComponent(
        QuestController questController,
        MovementController movementController,
        CombatController combatController,
        GatheringController gatheringController,
        QuestFunctions questFunctions,
        ICommandManager commandManager,
        Configuration configuration,
        QuestRegistry questRegistry,
        PriorityWindow priorityWindow,
        IChatGui chatGui)
    {
        _questController = questController;
        _movementController = movementController;
        _combatController = combatController;
        _gatheringController = gatheringController;
        _questFunctions = questFunctions;
        _commandManager = commandManager;
        _configuration = configuration;
        _questRegistry = questRegistry;
        _priorityWindow = priorityWindow;
        _chatGui = chatGui;
    }

    public event EventHandler? Reload;

    public void Draw(bool isMinimized)
    {
        var currentQuestDetails = _questController.CurrentQuestDetails;
        QuestController.QuestProgress? currentQuest = currentQuestDetails?.Progress;
        QuestController.ECurrentQuestType? currentQuestType = currentQuestDetails?.Type;
        if (currentQuest != null)
        {
            DrawQuestNames(currentQuest, currentQuestType);
            var questWork = DrawQuestWork(currentQuest, isMinimized);

            if (_combatController.IsRunning)
                ImGui.TextColored(ImGuiColors.DalamudOrange, "战斗模块工作中");
            else if (_questController.CurrentTaskState is { } currentTaskState)
            {
                using var _ = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudOrange);
                ImGui.TextUnformatted(currentTaskState);
            }
            else
            {
                using var _ = ImRaii.Disabled();
                ImGui.TextUnformatted(_questController.DebugState ?? string.Empty);
            }

            QuestSequence? currentSequence = currentQuest.Quest.FindSequence(currentQuest.Sequence);
            QuestStep? currentStep = currentSequence?.FindStep(currentQuest.Step);
            if (!isMinimized)
            {
                using (var color = new ImRaii.Color())
                {
                    bool colored = currentStep is
                    {
                        InteractionType: EInteractionType.Instruction or EInteractionType.WaitForManualProgress
                        or EInteractionType.Snipe
                    };
                    if (colored)
                        color.Push(ImGuiCol.Text, ImGuiColors.DalamudOrange);

                    ImGui.TextUnformatted(currentStep?.Comment ??
                                          currentSequence?.Comment ?? currentQuest.Quest.Root.Comment ?? string.Empty);
                }

                //var nextStep = _questController.GetNextStep();
                //ImGui.BeginDisabled(nextStep.Step == null);
                ImGui.Text(_questController.ToStatString());
                //ImGui.EndDisabled();
            }

            DrawQuestButtons(currentQuest, currentStep, questWork, isMinimized);

            DrawSimulationControls();
        }
        else
        {
            ImGui.Text("空闲中");
            if (!isMinimized)
                ImGui.TextColored(ImGuiColors.DalamudGrey, $"已支持 {_questRegistry.Count} 个任务");

            if (ImGuiComponents.IconButton(FontAwesomeIcon.Stop))
            {
                _movementController.Stop();
                _questController.Stop("Manual (no active quest)");
                _gatheringController.Stop("Manual (no active quest)");
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.SortAmountDown))
                _priorityWindow.Toggle();
        }
    }

    private void DrawQuestNames(QuestController.QuestProgress currentQuest,
        QuestController.ECurrentQuestType? currentQuestType)
    {
        if (currentQuestType == QuestController.ECurrentQuestType.Simulated)
        {
            using var _ = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
            ImGui.TextUnformatted(
                $"Simulated Quest: {Shorten(currentQuest.Quest.Info.Name)} ({currentQuest.Quest.Id}) / {currentQuest.Sequence} / {currentQuest.Step}");
        }
        else if (currentQuestType == QuestController.ECurrentQuestType.Gathering)
        {
            using var _ = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.ParsedGold);
            ImGui.TextUnformatted(
                $"Gathering: {Shorten(currentQuest.Quest.Info.Name)} ({currentQuest.Quest.Id}) / {currentQuest.Sequence} / {currentQuest.Step}");
        }
        else
        {
            var startedQuest = _questController.StartedQuest;
            if (startedQuest != null)
            {
                if (startedQuest.Quest.Source == Quest.ESource.UserDirectory)
                {
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.TextColored(ImGuiColors.DalamudOrange, FontAwesomeIcon.FilePen.ToIconString());
                    ImGui.PopFont();
                    ImGui.SameLine(0);

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(
                            "This quest is loaded from your 'pluginConfigs\\Questionable\\Quests' directory.\nThis gets loaded even if Questionable ships with a newer/different version of the quest.");
                }

                ImGui.TextUnformatted(
                    $"任务： {Shorten(startedQuest.Quest.Info.Name)} ({startedQuest.Quest.Id}) / {startedQuest.Sequence} / {startedQuest.Step}");

                if (startedQuest.Quest.Root.Disabled)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.DalamudRed, "Disabled");
                }

                if (_configuration.Advanced.AdditionalStatusInformation && _questController.IsInterruptible())
                {
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.DalamudYellow, SeIconChar.Hyadelyn.ToIconString());
                    if (ImGui.IsItemHovered())
                    {
                        using var tooltip = ImRaii.Tooltip();
                        if (tooltip)
                        {
                            ImGui.Text("该任务序列从传送到一处以太之光开始。");
                            ImGui.Text("某些优先任务（例如职业任务）可能会在当前任务之前，由插件自动优先完成。");
                            ImGui.Separator();
                            ImGui.Text("可用的优先任务：");

                            List<ElementId> priorityQuests = _questFunctions.GetNextPriorityQuestsThatCanBeAccepted();
                            if (priorityQuests.Count > 0)
                            {
                                foreach (var questId in priorityQuests)
                                {
                                    if (_questRegistry.TryGetQuest(questId, out var quest))
                                        ImGui.BulletText($"{quest.Info.Name} ({questId})");
                                }
                            }
                            else
                                ImGui.BulletText("(无)");
                        }
                    }
                }
            }

            var nextQuest = _questController.NextQuest;
            if (nextQuest != null)
            {
                using var _ = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudYellow);
                ImGui.TextUnformatted(
                    $"Next Quest: {Shorten(nextQuest.Quest.Info.Name)} ({nextQuest.Quest.Id}) / {nextQuest.Sequence} / {nextQuest.Step}");
            }
        }
    }

    private QuestProgressInfo? DrawQuestWork(QuestController.QuestProgress currentQuest, bool isMinimized)
    {
        var questWork = _questFunctions.GetQuestProgressInfo(currentQuest.Quest.Id);

        if (questWork != null)
        {
            if (isMinimized)
                return questWork;


            Vector4 color;
            unsafe
            {
                var ptr = ImGui.GetStyleColorVec4(ImGuiCol.TextDisabled);
                if (ptr != null)
                    color = *ptr;
                else
                    color = ImGuiColors.ParsedOrange;
            }

            using var styleColor = ImRaii.PushColor(ImGuiCol.Text, color);
            ImGui.Text($"{questWork}");

            if (ImGui.IsItemClicked())
            {
                string progressText = MultipleWhitespaceRegex().Replace(questWork.ToString(), " ");
                ImGui.SetClipboardText(progressText);
                _chatGui.Print($"'{progressText}' 已经复制到剪切板");
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.Text(FontAwesomeIcon.Copy.ToIconString());
                ImGui.PopFont();
            }

            if (currentQuest.Quest.Id is LeveId || currentQuest.Quest.Info.AlliedSociety != EAlliedSociety.None)
            {
                ImGui.SameLine();
                ImGui.Text($"/ {questWork.ClassJob}");
            }
        }
        else if (currentQuest.Quest.Id is QuestId)
        {
            using var disabled = ImRaii.Disabled();

            if (currentQuest.Quest.Id == _questController.NextQuest?.Quest.Id)
                ImGui.TextUnformatted("(故事线中的下一个任务尚未接取)");
            else
                ImGui.TextUnformatted("(未接取)");

        }

        return questWork;
    }

    private void DrawQuestButtons(QuestController.QuestProgress currentQuest, QuestStep? currentStep,
        QuestProgressInfo? questProgressInfo, bool isMinimized)
    {
        ImGui.BeginDisabled(_questController.IsRunning);
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Play))
        {
            // if we haven't accepted this quest, mark it as next quest so that we can optionally use aetherytes to travel
            if (questProgressInfo == null)
                _questController.SetNextQuest(currentQuest.Quest);

            _questController.Start("UI start");
        }

        if (!isMinimized)
        {
            ImGui.SameLine();

            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.StepForward, "Step"))
            {
                _questController.StartSingleStep("UI step");
            }
        }

        ImGui.EndDisabled();
        ImGui.SameLine();

        if (ImGuiComponents.IconButton(FontAwesomeIcon.Stop))
        {
            _movementController.Stop();
            _questController.Stop("UI stop");
            _gatheringController.Stop("UI stop");
        }

        if (isMinimized)
        {
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.RedoAlt))
                Reload?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            bool lastStep = currentStep ==
                            currentQuest.Quest.FindSequence(currentQuest.Sequence)?.Steps.LastOrDefault();
            bool colored = currentStep != null
                           && !lastStep
                           && currentStep.InteractionType == EInteractionType.Instruction
                           && _questController.HasCurrentTaskMatching<WaitAtEnd.WaitNextStepOrSequence>(out _);

            ImGui.BeginDisabled(lastStep);
            if (colored)
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.ParsedGreen);
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.ArrowCircleRight, "Skip"))
            {
                _movementController.Stop();
                _questController.Skip(currentQuest.Quest.Id, currentQuest.Sequence);
            }

            if (colored)
                ImGui.PopStyleColor();
            ImGui.EndDisabled();

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.SortAmountDown))
                _priorityWindow.Toggle();

            if (_commandManager.Commands.TryGetValue("/questinfo", out var commandInfo))
            {
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Atlas))
                    _commandManager.DispatchCommand("/questinfo",
                        currentQuest.Quest.Id.ToString() ?? string.Empty, commandInfo);
            }
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
                _questController.SimulateQuest(null, 0, 0);

                _movementController.Stop();
                _questController.Stop("ClearSim");
            }
        }
    }

    private static string Shorten(string text)
    {
        if (text.Length > 35)
            return string.Concat(text.AsSpan(0, 30).Trim(), ((SeIconChar)57434).ToIconString());

        return text;
    }
}
