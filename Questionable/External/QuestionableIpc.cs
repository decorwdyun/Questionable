﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using JetBrains.Annotations;
using Questionable.Controller;
using Questionable.Functions;
using Questionable.Model.Questing;
using Questionable.Windows.QuestComponents;

namespace Questionable.External;

internal sealed class QuestionableIpc : IDisposable
{
    private const string IpcIsRunning = "Questionable.IsRunning";
    private const string IpcGetCurrentQuestId = "Questionable.GetCurrentQuestId";
    private const string IpcGetCurrentStepData = "Questionable.GetCurrentStepData";
    private const string IpcGetCurrentlyActiveEventQuests = "Questionable.GetCurrentlyActiveEventQuests";
    private const string IpcStartQuest = "Questionable.StartQuest";
    private const string IpcStartSingleQuest = "Questionable.StartSingleQuest";
    private const string IpcIsQuestLocked = "Questionable.IsQuestLocked";

    private readonly QuestController _questController;
    private readonly QuestRegistry _questRegistry;
    private readonly QuestFunctions _questFunctions;

    private readonly ICallGateProvider<bool> _isRunning;
    private readonly ICallGateProvider<string?> _getCurrentQuestId;
    private readonly ICallGateProvider<StepData?> _getCurrentStepData;
    private readonly ICallGateProvider<List<string>> _getCurrentlyActiveEventQuests;
    private readonly ICallGateProvider<string, bool> _startQuest;
    private readonly ICallGateProvider<string, bool> _startSingleQuest;
    private readonly ICallGateProvider<string, bool> _isQuestLocked;

    public QuestionableIpc(
        QuestController questController,
        EventInfoComponent eventInfoComponent,
        QuestRegistry questRegistry,
        QuestFunctions questFunctions,
        IDalamudPluginInterface pluginInterface)
    {
        _questController = questController;
        _questRegistry = questRegistry;
        _questFunctions = questFunctions;

        _isRunning = pluginInterface.GetIpcProvider<bool>(IpcIsRunning);
        _isRunning.RegisterFunc(() =>
            questController.AutomationType != QuestController.EAutomationType.Manual || questController.IsRunning);

        _getCurrentQuestId = pluginInterface.GetIpcProvider<string?>(IpcGetCurrentQuestId);
        _getCurrentQuestId.RegisterFunc(() => questController.CurrentQuest?.Quest.Id.ToString());

        _getCurrentStepData = pluginInterface.GetIpcProvider<StepData?>(IpcGetCurrentStepData);
        _getCurrentStepData.RegisterFunc(GetStepData);

        _getCurrentlyActiveEventQuests =
            pluginInterface.GetIpcProvider<List<string>>(IpcGetCurrentlyActiveEventQuests);
        _getCurrentlyActiveEventQuests.RegisterFunc(() =>
            eventInfoComponent.GetCurrentlyActiveEventQuests().Select(q => q.ToString()).ToList());

        _startQuest = pluginInterface.GetIpcProvider<string, bool>(IpcStartQuest);
        _startQuest.RegisterFunc(questId => StartQuest(questId, false));

        _startSingleQuest = pluginInterface.GetIpcProvider<string, bool>(IpcStartSingleQuest);
        _startSingleQuest.RegisterFunc(questId => StartQuest(questId, true));
        //_startSingleQuest.RegisterFunc((questId) => StartQuest(questController, questRegistry, questId, true));

        _isQuestLocked = pluginInterface.GetIpcProvider<string, bool>(IpcIsQuestLocked);
        _isQuestLocked.RegisterFunc((questId) => IsQuestLocked(questId));
    }

    private bool StartQuest(string questId, bool single)
    {
        if (ElementId.TryFromString(questId, out var elementId) && elementId != null &&
            _questRegistry.TryGetQuest(elementId, out var quest))
        {
            _questController.SetNextQuest(quest);
            if (single)
                _questController.StartSingleQuest("IPCQuestSelection");
            else
                _questController.Start("IPCQuestSelection");
            return true;
        }

        return false;
    }

    private StepData? GetStepData()
    {
        var progress = _questController.CurrentQuest;
        if (progress == null)
            return null;

        string? questId = progress.Quest.Id.ToString();
        if (questId == null)
            return null;

        QuestStep? step = progress.Quest.FindSequence(progress.Sequence)?.FindStep(progress.Step);
        if (step == null)
            return null;

        return new StepData
        {
            QuestId = questId,
            Sequence = progress.Sequence,
            Step = progress.Step,
            InteractionType = step.InteractionType.ToString(),
            Position = step.Position,
            TerritoryId = step.TerritoryId
        };
    }

    private bool IsQuestLocked(string questId)
    {
        if (ElementId.TryFromString(questId, out var elementId) && elementId != null &&
            _questRegistry.TryGetQuest(elementId, out var quest))
        {
            return _questFunctions.IsQuestLocked(elementId);
        }
        return true;
    }

    public void Dispose()
    {
        _startSingleQuest.UnregisterFunc();
        _startQuest.UnregisterFunc();
        _getCurrentlyActiveEventQuests.UnregisterFunc();
        _getCurrentStepData.UnregisterFunc();
        _getCurrentQuestId.UnregisterFunc();
        _isRunning.UnregisterFunc();
    }

    [UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
    public sealed class StepData
    {
        public required string QuestId { get; init; }
        public required byte Sequence { get; init; }
        public required int Step { get; init; }
        public required string InteractionType { get; init; }
        public required Vector3? Position { get; init; }
        public required ushort TerritoryId { get; init; }
    }
}
