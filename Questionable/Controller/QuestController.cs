using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps;
using Questionable.External;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller;

internal sealed class QuestController
{
    private readonly IClientState _clientState;
    private readonly GameFunctions _gameFunctions;
    private readonly MovementController _movementController;
    private readonly ILogger<QuestController> _logger;
    private readonly QuestRegistry _questRegistry;
    private readonly IKeyState _keyState;
    private readonly Configuration _configuration;
    private readonly YesAlreadyIpc _yesAlreadyIpc;
    private readonly IReadOnlyList<ITaskFactory> _taskFactories;

    private readonly object _lock = new();

    private readonly Queue<ITask> _taskQueue = new();
    private ITask? _currentTask;
    private bool _automatic;

    public QuestController(
        IClientState clientState,
        GameFunctions gameFunctions,
        MovementController movementController,
        ILogger<QuestController> logger,
        QuestRegistry questRegistry,
        IKeyState keyState,
        Configuration configuration,
        YesAlreadyIpc yesAlreadyIpc,
        IEnumerable<ITaskFactory> taskFactories)
    {
        _clientState = clientState;
        _gameFunctions = gameFunctions;
        _movementController = movementController;
        _logger = logger;
        _questRegistry = questRegistry;
        _keyState = keyState;
        _configuration = configuration;
        _yesAlreadyIpc = yesAlreadyIpc;
        _taskFactories = taskFactories.ToList().AsReadOnly();
    }


    public QuestProgress? CurrentQuest { get; set; }
    public SimulationProgress? SimulatedQuest { get; set; }
    public string? DebugState { get; private set; }
    public string? Comment { get; private set; }

    public void Reload()
    {
        lock (_lock)
        {
            CurrentQuest = null;
            DebugState = null;

            _questRegistry.Reload();
        }
    }

    public void Update()
    {
        UpdateCurrentQuest();

        if (_keyState[VirtualKey.ESCAPE])
        {
            if (_currentTask != null || _taskQueue.Count > 0)
            {
                Stop("ESC pressed");
                _movementController.Stop();
            }
        }

        if (CurrentQuest != null && CurrentQuest.Quest.Data.TerritoryBlacklist.Contains(_clientState.TerritoryType))
            return;

        // not verified to work
        if (_automatic && _currentTask == null && _taskQueue.Count == 0
            && CurrentQuest is { Sequence: 0, Step: 0 } or { Sequence: 0, Step: 255 }
            && DateTime.Now >= CurrentQuest.StepProgress.StartedAt.AddSeconds(15))
        {
            lock (_lock)
            {
                _logger.LogWarning("Quest accept apparently didn't work out, resetting progress");
                CurrentQuest = CurrentQuest with
                {
                    Step = 0
                };
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

            ushort currentQuestId;
            byte currentSequence;
            if (SimulatedQuest != null)
            {
                currentQuestId = SimulatedQuest.Quest.QuestId;
                currentSequence = SimulatedQuest.Sequence;
            }
            else
                (currentQuestId, currentSequence) = _gameFunctions.GetCurrentQuest();

            if (currentQuestId == 0)
            {
                if (CurrentQuest != null)
                {
                    _logger.LogInformation("No current quest, resetting data");
                    CurrentQuest = null;
                    Stop("Resetting current quest");
                }
            }
            else if (CurrentQuest == null || CurrentQuest.Quest.QuestId != currentQuestId)
            {
                if (_questRegistry.TryGetQuest(currentQuestId, out var quest))
                {
                    _logger.LogInformation("New quest: {QuestName}", quest.Name);
                    CurrentQuest = new QuestProgress(quest, currentSequence, 0);

                    bool continueAutomatically = _configuration.General.AutoAcceptNextQuest;

                    if (_clientState.LocalPlayer?.Level < quest.Level)
                        continueAutomatically = false;

                    Stop("Different Quest", continueAutomatically);
                }
                else if (CurrentQuest != null)
                {
                    _logger.LogInformation("No active quest anymore? Not sure what happened...");
                    CurrentQuest = null;
                    Stop("No active Quest");
                }

                return;
            }

            if (CurrentQuest == null)
            {
                DebugState = "No quest active";
                Comment = null;
                Stop("No quest active");
                return;
            }

            if (_gameFunctions.IsOccupied())
            {
                DebugState = "Occupied";
                return;
            }

            if (!_movementController.IsNavmeshReady)
            {
                DebugState = "Navmesh not ready";
                return;
            }
            else if (_movementController.IsPathfinding || _movementController.IsPathRunning)
            {
                DebugState = "Path is running";
                return;
            }

            if (CurrentQuest.Sequence != currentSequence)
            {
                CurrentQuest = CurrentQuest with { Sequence = currentSequence, Step = 0 };
                Stop("New sequence", continueIfAutomatic: true);
            }

            var q = CurrentQuest.Quest;
            var sequence = q.FindSequence(CurrentQuest.Sequence);
            if (sequence == null)
            {
                DebugState = "Sequence not found";
                Comment = null;
                Stop("Unknown sequence");
                return;
            }

            if (CurrentQuest.Step == 255)
            {
                DebugState = "Step completed";
                Comment = null;
                if (_currentTask != null || _taskQueue.Count > 0)
                    Stop("Step complete", continueIfAutomatic: true);
                return;
            }

            if (CurrentQuest.Step >= sequence.Steps.Count)
            {
                DebugState = "Step not found";
                Comment = null;
                Stop("Unknown step");
                return;
            }

            var step = sequence.Steps[CurrentQuest.Step];
            DebugState = null;
            Comment = step.Comment ?? sequence.Comment ?? q.Data.Comment;
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
            {
                CurrentQuest = CurrentQuest with
                {
                    Step = CurrentQuest.Step + 1,
                    StepProgress = new(DateTime.Now),
                };
            }
            else
            {
                CurrentQuest = CurrentQuest with
                {
                    Step = 255,
                    StepProgress = new(DateTime.Now),
                };
            }
        }

        if (shouldContinue && _automatic)
            ExecuteNextStep(true);
    }

    private void ClearTasksInternal()
    {
        _currentTask = null;

        if (_taskQueue.Count > 0)
            _taskQueue.Clear();

        _yesAlreadyIpc.RestoreYesAlready();
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
        }
        else if (_automatic)
        {
            _logger.LogInformation("Stopping automatic questing");
            _automatic = false;
        }
    }

    public void SimulateQuest(Quest? quest)
    {
        if (quest != null)
            SimulatedQuest = new SimulationProgress(quest, 0);
        else
            SimulatedQuest = null;
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

        (QuestSequence? seq, QuestStep? step) = GetNextStep();
        if (CurrentQuest == null || seq == null || step == null)
        {
            _logger.LogWarning("Could not retrieve next quest step, not doing anything [{QuestId}, {Sequence}, {Step}]",
                CurrentQuest?.Quest.QuestId, CurrentQuest?.Sequence, CurrentQuest?.Step);
            return;
        }

        _movementController.Stop();

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

    public sealed record QuestProgress(
        Quest Quest,
        byte Sequence,
        int Step,
        StepProgress StepProgress)
    {
        public QuestProgress(Quest quest, byte sequence, int step)
            : this(quest, sequence, step, new StepProgress(DateTime.Now))
        {
        }
    }

    public sealed record SimulationProgress(Quest Quest, byte Sequence);

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

    public sealed record StepProgress(
        DateTime StartedAt,
        int PointMenuCounter = 0);
}
