using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps;
using Questionable.Controller.Steps.Shared;
using Questionable.External;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller;

internal sealed class QuestController
{
    private readonly IClientState _clientState;
    private readonly GameFunctions _gameFunctions;
    private readonly MovementController _movementController;
    private readonly CombatController _combatController;
    private readonly ILogger<QuestController> _logger;
    private readonly QuestRegistry _questRegistry;
    private readonly IKeyState _keyState;
    private readonly IChatGui _chatGui;
    private readonly ICondition _condition;
    private readonly Configuration _configuration;
    private readonly YesAlreadyIpc _yesAlreadyIpc;
    private readonly IReadOnlyList<ITaskFactory> _taskFactories;

    private readonly object _lock = new();

    private QuestProgress? _startedQuest;
    private QuestProgress? _nextQuest;
    private QuestProgress? _simulatedQuest;
    private readonly Queue<ITask> _taskQueue = new();
    private ITask? _currentTask;
    private bool _automatic;

    public QuestController(
        IClientState clientState,
        GameFunctions gameFunctions,
        MovementController movementController,
        CombatController combatController,
        ILogger<QuestController> logger,
        QuestRegistry questRegistry,
        IKeyState keyState,
        IChatGui chatGui,
        ICondition condition,
        Configuration configuration,
        YesAlreadyIpc yesAlreadyIpc,
        IEnumerable<ITaskFactory> taskFactories)
    {
        _clientState = clientState;
        _gameFunctions = gameFunctions;
        _movementController = movementController;
        _combatController = combatController;
        _logger = logger;
        _questRegistry = questRegistry;
        _keyState = keyState;
        _chatGui = chatGui;
        _condition = condition;
        _configuration = configuration;
        _yesAlreadyIpc = yesAlreadyIpc;
        _taskFactories = taskFactories.ToList().AsReadOnly();
    }

    public (QuestProgress Progress, CurrentQuestType Type)? CurrentQuestDetails
    {
        get
        {
            if (_simulatedQuest != null)
                return (_simulatedQuest, CurrentQuestType.Simulated);
            else if (_nextQuest != null && _gameFunctions.IsReadyToAcceptQuest(_nextQuest.Quest.QuestId))
                return (_nextQuest, CurrentQuestType.Next);
            else if (_startedQuest != null)
                return (_startedQuest, CurrentQuestType.Normal);
            else
                return null;
        }
    }

    public QuestProgress? CurrentQuest => CurrentQuestDetails?.Progress;

    public QuestProgress? StartedQuest => _startedQuest;
    public QuestProgress? SimulatedQuest => _simulatedQuest;
    public QuestProgress? NextQuest => _nextQuest;

    public string? DebugState { get; private set; }

    public void Reload()
    {
        lock (_lock)
        {
            _logger.LogInformation("Reload, resetting curent quest progress");

            _startedQuest = null;
            _nextQuest = null;
            _simulatedQuest = null;

            DebugState = null;

            _questRegistry.Reload();
        }
    }

    public void Update()
    {
        UpdateCurrentQuest();

        if (!_clientState.IsLoggedIn || _condition[ConditionFlag.Unconscious])
        {
            if (_currentTask != null || _taskQueue.Count > 0)
            {
                Stop("HP = 0");
                _movementController.Stop();
                _combatController.Stop();
            }
        } else if (_keyState[VirtualKey.ESCAPE])
        {
            if (_currentTask != null || _taskQueue.Count > 0)
            {
                Stop("ESC pressed");
                _movementController.Stop();
                _combatController.Stop();
            }
        }

        if (CurrentQuest != null && CurrentQuest.Quest.Root.TerritoryBlacklist.Contains(_clientState.TerritoryType))
            return;

        if (_automatic && ((_currentTask == null && _taskQueue.Count == 0) ||
                           _currentTask is WaitAtEnd.WaitQuestAccepted)
                       && CurrentQuest is { Sequence: 0, Step: 0 } or { Sequence: 0, Step: 255 }
                       && DateTime.Now >= CurrentQuest.StepProgress.StartedAt.AddSeconds(15))
        {
            lock (_lock)
            {
                _logger.LogWarning("Quest accept apparently didn't work out, resetting progress");
                CurrentQuest.SetStep(0);
            }

            ExecuteNextStep(true);
            return;
        }

        UpdateCurrentTask();
    }

    private void UpdateCurrentQuest()
    {
        lock (_lock)
        {
            DebugState = null;

            if (_simulatedQuest == null && _nextQuest != null)
            {
                // if the quest is accepted, we no longer track it
                bool canUseNextQuest;
                if (_nextQuest.Quest.Info.IsRepeatable)
                    canUseNextQuest = !_gameFunctions.IsQuestAccepted(_nextQuest.Quest.QuestId);
                else
                    canUseNextQuest = !_gameFunctions.IsQuestAcceptedOrComplete(_nextQuest.Quest.QuestId);

                if (!canUseNextQuest)
                {
                    _logger.LogInformation("Next quest {QuestId} accepted or completed", _nextQuest.Quest.QuestId);
                    _nextQuest = null;
                }
            }

            QuestProgress? questToRun;
            byte currentSequence;
            if (_simulatedQuest != null)
            {
                currentSequence = _simulatedQuest.Sequence;
                questToRun = _simulatedQuest;
            }
            else if (_nextQuest != null && _gameFunctions.IsReadyToAcceptQuest(_nextQuest.Quest.QuestId))
            {
                questToRun = _nextQuest;
                currentSequence = _nextQuest.Sequence; // by definition, this should always be 0
                if (_nextQuest.Step == 0 && _currentTask == null && _taskQueue.Count == 0 && _automatic)
                    ExecuteNextStep(true);
            }
            else
            {
                (ushort currentQuestId, currentSequence) = _gameFunctions.GetCurrentQuest();
                if (currentQuestId == 0)
                {
                    if (_startedQuest != null)
                    {
                        _logger.LogInformation("No current quest, resetting data");
                        _startedQuest = null;
                        Stop("Resetting current quest");
                    }

                    questToRun = null;
                }
                else if (_startedQuest == null || _startedQuest.Quest.QuestId != currentQuestId)
                {
                    if (_questRegistry.TryGetQuest(currentQuestId, out var quest))
                    {
                        _logger.LogInformation("New quest: {QuestName}", quest.Info.Name);
                        _startedQuest = new QuestProgress(quest, currentSequence);

                        bool continueAutomatically = _configuration.General.AutoAcceptNextQuest;

                        if (_clientState.LocalPlayer?.Level < quest.Info.Level)
                            continueAutomatically = false;

                        Stop("Different Quest", continueAutomatically);
                    }
                    else if (_startedQuest != null)
                    {
                        _logger.LogInformation("No active quest anymore? Not sure what happened...");
                        _startedQuest = null;
                        Stop("No active Quest");
                    }

                    return;
                }
                else
                    questToRun = _startedQuest;
            }

            if (questToRun == null)
            {
                DebugState = "No quest active";
                Stop("No quest active");
                return;
            }

            if (_gameFunctions.IsOccupied())
            {
                DebugState = "Occupied";
                return;
            }

            if (_movementController.IsPathfinding)
            {
                DebugState = "Pathfinding is running";
                return;
            }

            if (_movementController.IsPathRunning)
            {
                DebugState = "Path is running";
                return;
            }

            if (questToRun.Sequence != currentSequence)
            {
                questToRun.SetSequence(currentSequence);
                Stop($"New sequence {questToRun == _startedQuest}/{_gameFunctions.GetCurrentQuestInternal()}",
                    continueIfAutomatic: true);
            }

            var q = questToRun.Quest;
            var sequence = q.FindSequence(questToRun.Sequence);
            if (sequence == null)
            {
                DebugState = "Sequence not found";
                Stop("Unknown sequence");
                return;
            }

            if (questToRun.Step == 255)
            {
                DebugState = "Step completed";
                if (_currentTask != null || _taskQueue.Count > 0)
                    Stop("Step complete", continueIfAutomatic: true);
                return;
            }

            if (questToRun.Step >= sequence.Steps.Count)
            {
                DebugState = "Step not found";
                Stop("Unknown step");
                return;
            }

            DebugState = null;
        }
    }

    public (QuestSequence? Sequence, QuestStep? Step) GetNextStep()
    {
        if (CurrentQuest == null)
            return (null, null);

        var q = CurrentQuest.Quest;
        var seq = q.FindSequence(CurrentQuest.Sequence);
        if (seq == null)
            return (null, null);

        if (CurrentQuest.Step >= seq.Steps.Count)
            return (null, null);

        return (seq, seq.Steps[CurrentQuest.Step]);
    }

    public void IncreaseStepCount(ushort? questId, int? sequence, bool shouldContinue = false)
    {
        lock (_lock)
        {
            (QuestSequence? seq, QuestStep? step) = GetNextStep();
            if (CurrentQuest == null || seq == null || step == null)
            {
                _logger.LogWarning("Unable to retrieve next quest step, not increasing step count");
                return;
            }

            if (questId != null && CurrentQuest.Quest.QuestId != questId)
            {
                _logger.LogWarning(
                    "Ignoring 'increase step count' for different quest (expected {ExpectedQuestId}, but we are at {CurrentQuestId}",
                    questId, CurrentQuest.Quest.QuestId);
                return;
            }

            if (sequence != null && seq.Sequence != sequence.Value)
            {
                _logger.LogWarning(
                    "Ignoring 'increase step count' for different sequence (expected {ExpectedSequence}, but we are at {CurrentSequence}",
                    sequence, seq.Sequence);
            }

            _logger.LogInformation("Increasing step count from {CurrentValue}", CurrentQuest.Step);
            if (CurrentQuest.Step + 1 < seq.Steps.Count)
                CurrentQuest.SetStep(CurrentQuest.Step + 1);
            else
                CurrentQuest.SetStep(255);
        }

        if (shouldContinue && _automatic)
            ExecuteNextStep(true);
    }

    private void ClearTasksInternal()
    {
        //_logger.LogDebug("Clearing task (internally)");
        _currentTask = null;

        if (_taskQueue.Count > 0)
            _taskQueue.Clear();

        _yesAlreadyIpc.RestoreYesAlready();
        _combatController.Stop();
    }

    public void Stop(string label, bool continueIfAutomatic = false)
    {
        using var scope = _logger.BeginScope(label);

        ClearTasksInternal();

        // reset task queue
        if (continueIfAutomatic && _automatic)
        {
            if (CurrentQuest?.Step is >= 0 and < 255)
                ExecuteNextStep(true);
            else
                _logger.LogInformation("Couldn't execute next step during Stop() call");
        }
        else if (_automatic)
        {
            _logger.LogInformation("Stopping automatic questing");
            _automatic = false;
            _nextQuest = null;
        }
    }

    public void SimulateQuest(Quest? quest)
    {
        _logger.LogInformation("SimulateQuest: {QuestId}", quest?.QuestId);
        if (quest != null)
            _simulatedQuest = new QuestProgress(quest);
        else
            _simulatedQuest = null;
    }

    public void SetNextQuest(Quest? quest)
    {
        _logger.LogInformation("NextQuest: {QuestId}", quest?.QuestId);
        if (quest != null)
            _nextQuest = new QuestProgress(quest);
        else
            _nextQuest = null;
    }

    private void UpdateCurrentTask()
    {
        if (_gameFunctions.IsOccupied())
            return;

        if (_currentTask == null)
        {
            if (_taskQueue.TryDequeue(out ITask? upcomingTask))
            {
                try
                {
                    _logger.LogInformation("Starting task {TaskName}", upcomingTask.ToString());
                    if (upcomingTask.Start())
                    {
                        _currentTask = upcomingTask;
                        return;
                    }
                    else
                    {
                        _logger.LogTrace("Task {TaskName} was skipped", upcomingTask.ToString());
                        return;
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to start task {TaskName}", upcomingTask.ToString());
                    _chatGui.PrintError(
                        $"[Questionable] Failed to start task '{upcomingTask}', please check /xllog for details.");
                    Stop("Task failed to start");
                    return;
                }
            }
            else
                return;
        }

        ETaskResult result;
        try
        {
            result = _currentTask.Update();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to update task {TaskName}", _currentTask.ToString());
            _chatGui.PrintError(
                $"[Questionable] Failed to update task '{_currentTask}', please check /xllog for details.");
            Stop("Task failed to update");
            return;
        }

        switch (result)
        {
            case ETaskResult.StillRunning:
                return;

            case ETaskResult.SkipRemainingTasksForStep:
                _logger.LogInformation("{Task} → {Result}, skipping remaining tasks for step",
                    _currentTask, result);
                _currentTask = null;

                while (_taskQueue.TryDequeue(out ITask? nextTask))
                {
                    if (nextTask is ILastTask)
                    {
                        _currentTask = nextTask;
                        return;
                    }
                }

                return;

            case ETaskResult.TaskComplete:
                _logger.LogInformation("{Task} → {Result}, remaining tasks: {RemainingTaskCount}",
                    _currentTask, result, _taskQueue.Count);

                if (_currentTask is WaitAtEnd.WaitQuestCompleted)
                    _simulatedQuest = null;

                _currentTask = null;

                // handled in next update
                return;

            case ETaskResult.NextStep:
                _logger.LogInformation("{Task} → {Result}", _currentTask, result);

                var lastTask = (ILastTask)_currentTask;
                _currentTask = null;
                IncreaseStepCount(lastTask.QuestId, lastTask.Sequence, true);
                return;

            case ETaskResult.End:
                _logger.LogInformation("{Task} → {Result}", _currentTask, result);
                _currentTask = null;
                Stop("Task end");
                return;
        }
    }

    public void ExecuteNextStep(bool automatic)
    {
        ClearTasksInternal();
        _automatic = automatic;

        if (TryPickPriorityQuest())
            _logger.LogInformation("Using priority quest over current quest");

        (QuestSequence? seq, QuestStep? step) = GetNextStep();
        if (CurrentQuest == null || seq == null || step == null)
        {
            _logger.LogWarning("Could not retrieve next quest step, not doing anything [{QuestId}, {Sequence}, {Step}]",
                CurrentQuest?.Quest.QuestId, CurrentQuest?.Sequence, CurrentQuest?.Step);
            return;
        }

        _movementController.Stop();
        _combatController.Stop();

        var newTasks = _taskFactories
            .SelectMany(x =>
            {
                IList<ITask> tasks = x.CreateAllTasks(CurrentQuest.Quest, seq, step).ToList();

                if (tasks.Count > 0 && _logger.IsEnabled(LogLevel.Trace))
                {
                    string factoryName = x.GetType().FullName ?? x.GetType().Name;
                    if (factoryName.Contains('.', StringComparison.Ordinal))
                        factoryName = factoryName[(factoryName.LastIndexOf('.') + 1)..];

                    _logger.LogTrace("Factory {FactoryName} created Task {TaskNames}",
                        factoryName, string.Join(", ", tasks.Select(y => y.ToString())));
                }

                return tasks;
            })
            .ToList();
        if (newTasks.Count == 0)
        {
            _logger.LogInformation("Nothing to execute for step?");
            return;
        }

        _logger.LogInformation("Tasks for {QuestId}, {Sequence}, {Step}: {Tasks}",
            CurrentQuest.Quest.QuestId, seq.Sequence, seq.Steps.IndexOf(step),
            string.Join(", ", newTasks.Select(x => x.ToString())));
        foreach (var task in newTasks)
            _taskQueue.Enqueue(task);
    }

    public IList<string> GetRemainingTaskNames() =>
        _taskQueue.Select(x => x.ToString() ?? "?").ToList();

    public string ToStatString()
    {
        return _currentTask == null ? $"- (+{_taskQueue.Count})" : $"{_currentTask} (+{_taskQueue.Count})";
    }

    public bool HasCurrentTaskMatching<T>() =>
        _currentTask is T;

    public bool IsRunning => _currentTask != null || _taskQueue.Count > 0;

    public sealed class QuestProgress
    {
        public Quest Quest { get; }
        public byte Sequence { get; private set; }
        public int Step { get; private set; }
        public StepProgress StepProgress { get; private set; } = new(DateTime.Now);

        public QuestProgress(Quest quest, byte sequence = 0)
        {
            Quest = quest;
            SetSequence(sequence);
        }

        public void SetSequence(byte sequence)
        {
            Sequence = sequence;
            SetStep(0);
        }

        public void SetStep(int step)
        {
            Step = step;
            StepProgress = new StepProgress(DateTime.Now);
        }

        public void IncreasePointMenuCounter()
        {
            StepProgress = StepProgress with
            {
                PointMenuCounter = StepProgress.PointMenuCounter + 1,
            };
        }
    }

    public void Skip(ushort questQuestId, byte currentQuestSequence)
    {
        lock (_lock)
        {
            if (_currentTask is ISkippableTask)
                _currentTask = null;
            else if (_currentTask != null)
            {
                _currentTask = null;
                while (_taskQueue.Count > 0)
                {
                    var task = _taskQueue.Dequeue();
                    if (task is ISkippableTask)
                        return;
                }

                if (_taskQueue.Count == 0)
                {
                    Stop("Skip");
                    IncreaseStepCount(questQuestId, currentQuestSequence);
                }
            }
            else
            {
                Stop("SkipNx");
                IncreaseStepCount(questQuestId, currentQuestSequence);
            }
        }
    }

    public void SkipSimulatedTask()
    {
        _currentTask = null;
    }

    public bool IsInterruptible()
    {
        var details = CurrentQuestDetails;
        if (details == null)
            return false;

        var (currentQuest, type) = details.Value;
        if (type != CurrentQuestType.Normal)
            return false;

        QuestSequence? currentSequence = currentQuest.Quest.FindSequence(currentQuest.Sequence);
        QuestStep? currentStep = currentSequence?.FindStep(currentQuest.Step);

        // TODO Should this check that all previous steps have CompletionFlags so that we avoid running to places
        // no longer relevant for the non-priority quest (after we're done with the priority quest)?
        return currentStep?.AetheryteShortcut != null;
    }

    public bool TryPickPriorityQuest()
    {
        if (!IsInterruptible())
            return false;

        ushort[] priorityQuests =
        [
            1157, // Garuda (Hard)
            1158, // Titan (Hard)
        ];

        foreach (var questId in priorityQuests)
        {
            if (_gameFunctions.IsReadyToAcceptQuest(questId) && _questRegistry.TryGetQuest(questId, out var quest))
            {
                SetNextQuest(quest);
                _chatGui.Print(
                    $"[Questionable] Picking up quest '{quest.Info.Name}' as a priority over current main story/side quests.");
                return true;
            }
        }

        return false;
    }

    public sealed record StepProgress(
        DateTime StartedAt,
        int PointMenuCounter = 0);

    public enum CurrentQuestType
    {
        Normal,
        Next,
        Simulated,
    }
}
