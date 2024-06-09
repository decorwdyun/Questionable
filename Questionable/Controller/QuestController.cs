using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using FFXIVClientStructs.FFXIV.Client.Game;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps;
using Questionable.Data;
using Questionable.External;
using Questionable.Model;
using Questionable.Model.V1;
using Questionable.Model.V1.Converter;

namespace Questionable.Controller;

internal sealed class QuestController
{
    private readonly IClientState _clientState;
    private readonly GameFunctions _gameFunctions;
    private readonly MovementController _movementController;
    private readonly ILogger<QuestController> _logger;
    private readonly QuestRegistry _questRegistry;
    private readonly IKeyState _keyState;
    private readonly IReadOnlyList<ITaskFactory> _taskFactories;

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
        IEnumerable<ITaskFactory> taskFactories)
    {
        _clientState = clientState;
        _gameFunctions = gameFunctions;
        _movementController = movementController;
        _logger = logger;
        _questRegistry = questRegistry;
        _keyState = keyState;
        _taskFactories = taskFactories.ToList().AsReadOnly();
    }


    public QuestProgress? CurrentQuest { get; set; }
    public string? DebugState { get; private set; }
    public string? Comment { get; private set; }

    public void Reload()
    {
        CurrentQuest = null;
        DebugState = null;

        _questRegistry.Reload();
    }

    public void Update()
    {
        UpdateCurrentQuest();

        if (_keyState[VirtualKey.ESCAPE])
        {
            Stop();
            _movementController.Stop();
        }

        if (CurrentQuest != null && CurrentQuest.Quest.Data.TerritoryBlacklist.Contains(_clientState.TerritoryType))
            return;

        UpdateCurrentTask();
    }

    private void UpdateCurrentQuest()
    {
        DebugState = null;

        (ushort currentQuestId, byte currentSequence) = _gameFunctions.GetCurrentQuest();
        if (currentQuestId == 0)
        {
            if (CurrentQuest != null)
            {
                _logger.LogInformation("No current quest, resetting data");
                CurrentQuest = null;
                Stop();
            }
        }
        else if (CurrentQuest == null || CurrentQuest.Quest.QuestId != currentQuestId)
        {
            if (_questRegistry.TryGetQuest(currentQuestId, out var quest))
            {
                _logger.LogInformation("New quest: {QuestName}", quest.Name);
                CurrentQuest = new QuestProgress(quest, currentSequence, 0);
            }
            else if (CurrentQuest != null)
            {
                _logger.LogInformation("No active quest anymore? Not sure what happened...");
                CurrentQuest = null;
            }

            Stop();
            return;
        }

        if (CurrentQuest == null)
        {
            DebugState = "No quest active";
            Comment = null;
            Stop();
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

            bool automatic = _automatic;
            Stop();
            if (automatic)
                ExecuteNextStep(true);
        }

        var q = CurrentQuest.Quest;
        var sequence = q.FindSequence(CurrentQuest.Sequence);
        if (sequence == null)
        {
            DebugState = "Sequence not found";
            Comment = null;
            Stop();
            return;
        }

        if (CurrentQuest.Step == 255)
        {
            DebugState = "Step completed";
            Comment = null;
            Stop();
            return;
        }

        if (CurrentQuest.Step >= sequence.Steps.Count)
        {
            DebugState = "Step not found";
            Comment = null;
            Stop();
            return;
        }

        var step = sequence.Steps[CurrentQuest.Step];
        DebugState = null;
        Comment = step.Comment ?? sequence.Comment ?? q.Data.Comment;
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

    public void IncreaseStepCount(bool shouldContinue = false)
    {
        (QuestSequence? seq, QuestStep? step) = GetNextStep();
        if (CurrentQuest == null || seq == null || step == null)
        {
            _logger.LogWarning("Unable to retrieve next quest step, not increasing step count");
            return;
        }

        _logger.LogInformation("Increasing step count from {CurrentValue}", CurrentQuest.Step);
        if (CurrentQuest.Step + 1 < seq.Steps.Count)
        {
            CurrentQuest = CurrentQuest with
            {
                Step = CurrentQuest.Step + 1,
                StepProgress = new()
            };
        }
        else
        {
            CurrentQuest = CurrentQuest with
            {
                Step = 255,
                StepProgress = new()
            };
        }


        if (shouldContinue && _automatic)
            ExecuteNextStep(true);
    }

    public void IncreaseDialogueChoicesSelected()
    {
        (QuestSequence? seq, QuestStep? step) = GetNextStep();
        if (CurrentQuest == null || seq == null || step == null)
        {
            _logger.LogWarning("Unable to retrieve next quest step, not increasing dialogue choice count");
            return;
        }

        CurrentQuest = CurrentQuest with
        {
            StepProgress = CurrentQuest.StepProgress with
            {
                DialogueChoicesSelected = CurrentQuest.StepProgress.DialogueChoicesSelected + 1
            }
        };

        /* TODO Is this required?
        if (CurrentQuest.StepProgress.DialogueChoicesSelected >= step.DialogueChoices.Count)
            IncreaseStepCount();
            */
    }

    public void Stop()
    {
        _currentTask = null;

        if (_taskQueue.Count > 0)
            _taskQueue.Clear();

        // reset task queue
        _automatic = false;
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
                    Stop();
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
            Stop();
            return;
        }

        switch (result)
        {
            case ETaskResult.StillRunning:
                return;

            case ETaskResult.SkipRemainingTasksForStep:
                _logger.LogInformation("Result: {Result}, skipping remaining tasks for step", result);
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
                _logger.LogInformation("Result: {Result}, remaining tasks: {RemainingTaskCount}", result,
                    _taskQueue.Count);
                _currentTask = null;

                // handled in next update
                return;

            case ETaskResult.NextStep:
                _logger.LogInformation("Result: {Result}", result);
                IncreaseStepCount(true);
                return;

            case ETaskResult.End:
                _logger.LogInformation("Result: {Result}", result);
                Stop();
                return;
        }
    }

    public void ExecuteNextStep(bool automatic)
    {
        Stop();
        _automatic = automatic;

        (QuestSequence? seq, QuestStep? step) = GetNextStep();
        if (CurrentQuest == null || seq == null || step == null)
        {
            _logger.LogWarning("Could not retrieve next quest step, not doing anything");
            return;
        }

        var newTasks = _taskFactories
            .SelectMany(x =>
            {
                IList<ITask> tasks = x.CreateAllTasks(CurrentQuest.Quest, seq, step).ToList();

                if (_logger.IsEnabled(LogLevel.Trace))
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

        foreach (var task in newTasks)
            _taskQueue.Enqueue(task);
    }

    public IList<string> GetRemainingTaskNames() =>
        _taskQueue.Select(x => x.ToString() ?? "?").ToList();

    public string ToStatString()
    {
        return _currentTask == null ? $"- (+{_taskQueue.Count})" : $"{_currentTask} (+{_taskQueue.Count})";
    }

    public sealed record QuestProgress(
        Quest Quest,
        byte Sequence,
        int Step,
        StepProgress StepProgress)
    {
        public QuestProgress(Quest quest, byte sequence, int step)
            : this(quest, sequence, step, new StepProgress())
        {
        }
    }

    // TODO is this still required?
    public sealed record StepProgress(
        int DialogueChoicesSelected = 0);
}
