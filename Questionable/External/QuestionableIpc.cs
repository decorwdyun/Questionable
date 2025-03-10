using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using JetBrains.Annotations;
using Questionable.Controller;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;
using Questionable.Windows;
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
    private const string IpcImportQuestPriority = "Questionable.ImportQuestPriority";
    private const string IpcClearQuestPriority = "Questionable.ClearQuestPriority";
    private const string IpcAddQuestPriority = "Questionable.AddQuestPriority";
    private const string IpcInsertQuestPriority = "Questionable.InsertQuestPriority";
    private const string IpcExportQuestPriority = "Questionable.ExportQuestPriority";

    private readonly QuestController _questController;
    private readonly QuestRegistry _questRegistry;
    private readonly QuestFunctions _questFunctions;
    private readonly PriorityWindow _priorityWindow;

    private readonly ICallGateProvider<bool> _isRunning;
    private readonly ICallGateProvider<string?> _getCurrentQuestId;
    private readonly ICallGateProvider<StepData?> _getCurrentStepData;
    private readonly ICallGateProvider<List<string>> _getCurrentlyActiveEventQuests;
    private readonly ICallGateProvider<string, bool> _startQuest;
    private readonly ICallGateProvider<string, bool> _startSingleQuest;
    private readonly ICallGateProvider<string, bool> _isQuestLocked;
    private readonly ICallGateProvider<string, bool> _importQuestPriority;
    private readonly ICallGateProvider<string, bool> _addQuestPriority;
    private readonly ICallGateProvider<bool> _clearQuestPriority;
    private readonly ICallGateProvider<int, string, bool> _insertQuestPriority;
    private readonly ICallGateProvider<string> _exportQuestPriority;

    public QuestionableIpc(
        QuestController questController,
        EventInfoComponent eventInfoComponent,
        QuestRegistry questRegistry,
        QuestFunctions questFunctions,
        PriorityWindow priorityWindow,
        IDalamudPluginInterface pluginInterface)
    {
        _questController = questController;
        _questRegistry = questRegistry;
        _questFunctions = questFunctions;
        _priorityWindow = priorityWindow;

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

        _isQuestLocked = pluginInterface.GetIpcProvider<string, bool>(IpcIsQuestLocked);
        _isQuestLocked.RegisterFunc((questId) => IsQuestLocked(questId));

        _importQuestPriority = pluginInterface.GetIpcProvider<string, bool>(IpcImportQuestPriority);
        _importQuestPriority.RegisterFunc((encodedQuestPriority) => ImportQuestPriority(encodedQuestPriority));

        _importQuestPriority = pluginInterface.GetIpcProvider<string, bool>(IpcAddQuestPriority);
        _importQuestPriority.RegisterFunc((questId) => AddQuestPriority(questId));

        _clearQuestPriority = pluginInterface.GetIpcProvider<bool>(IpcClearQuestPriority);
        _clearQuestPriority.RegisterFunc(ClearQuestPriority);

        _insertQuestPriority = pluginInterface.GetIpcProvider<int, string, bool>(IpcInsertQuestPriority);
        _insertQuestPriority.RegisterFunc((index, questId) => InsertQuestPriority(index, questId));

        _exportQuestPriority = pluginInterface.GetIpcProvider<string>(IpcExportQuestPriority);
        _exportQuestPriority.RegisterFunc(_priorityWindow.EncodeQuestPriority);
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
            _questRegistry.TryGetQuest(elementId, out _))
        {
            return _questFunctions.IsQuestLocked(elementId);
        }
        return true;
    }

    private bool ImportQuestPriority(string encodedQuestPriority)
    {
        List<ElementId> questElements = PriorityWindow.DecodeQuestPriority(encodedQuestPriority);
        _questController.ImportQuestPriority(questElements);
        return true;
    }

    private bool ClearQuestPriority()
    {
        _questController.ClearQuestPriority();
        return true;
    }
    
    private bool AddQuestPriority(string questId)
    {
        if (ElementId.TryFromString(questId, out var elementId) && elementId != null &&
            _questRegistry.TryGetQuest(elementId, out var quest))
        {
            return _questController.AddQuestPriority(elementId);
        }
        return true;
    }
    
    private bool InsertQuestPriority(int index, string questId)
    {
        if (ElementId.TryFromString(questId, out var elementId) && elementId != null &&
            _questRegistry.TryGetQuest(elementId, out var quest))
        {
            return _questController.InsertQuestPriority(index, elementId);
        }
        return true;
    }

    public void Dispose()
    {
        _isQuestLocked.UnregisterFunc();
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
