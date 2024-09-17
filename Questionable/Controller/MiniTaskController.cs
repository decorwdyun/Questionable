using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps;
using Questionable.Controller.Steps.Shared;

namespace Questionable.Controller;

internal abstract class MiniTaskController<T>
{
    protected readonly IChatGui _chatGui;
    protected readonly ILogger<T> _logger;
    protected readonly TaskQueue _taskQueue = new();

    protected MiniTaskController(IChatGui chatGui, ILogger<T> logger)
    {
        _chatGui = chatGui;
        _logger = logger;
    }

    protected virtual void UpdateCurrentTask()
    {
        if (_taskQueue.CurrentTask == null)
        {
            if (_taskQueue.TryDequeue(out ITask? upcomingTask))
            {
                try
                {
                    _logger.LogInformation("Starting task {TaskName}", upcomingTask.ToString());
                    if (upcomingTask.Start())
                    {
                        _taskQueue.CurrentTask = upcomingTask;
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
            result = _taskQueue.CurrentTask.Update();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to update task {TaskName}", _taskQueue.CurrentTask.ToString());
            _chatGui.PrintError(
                $"[Questionable] Failed to update task '{_taskQueue.CurrentTask}', please check /xllog for details.");
            Stop("Task failed to update");
            return;
        }

        switch (result)
        {
            case ETaskResult.StillRunning:
                return;

            case ETaskResult.SkipRemainingTasksForStep:
                _logger.LogInformation("{Task} → {Result}, skipping remaining tasks for step",
                    _taskQueue.CurrentTask, result);
                _taskQueue.CurrentTask = null;

                while (_taskQueue.TryDequeue(out ITask? nextTask))
                {
                    if (nextTask is ILastTask or Gather.SkipMarker)
                    {
                        _taskQueue.CurrentTask = nextTask;
                        return;
                    }
                }

                return;

            case ETaskResult.TaskComplete:
                _logger.LogInformation("{Task} → {Result}, remaining tasks: {RemainingTaskCount}",
                    _taskQueue.CurrentTask, result, _taskQueue.RemainingTasks.Count());

                OnTaskComplete(_taskQueue.CurrentTask);

                _taskQueue.CurrentTask = null;

                // handled in next update
                return;

            case ETaskResult.NextStep:
                _logger.LogInformation("{Task} → {Result}", _taskQueue.CurrentTask, result);

                var lastTask = (ILastTask)_taskQueue.CurrentTask;
                _taskQueue.CurrentTask = null;

                OnNextStep(lastTask);
                return;

            case ETaskResult.End:
                _logger.LogInformation("{Task} → {Result}", _taskQueue.CurrentTask, result);
                _taskQueue.CurrentTask = null;
                Stop("Task end");
                return;
        }
    }

    protected virtual void OnTaskComplete(ITask task)
    {
    }

    protected virtual void OnNextStep(ILastTask task)
    {

    }

    public abstract void Stop(string label);

    public virtual IList<string> GetRemainingTaskNames() =>
        _taskQueue.RemainingTasks.Select(x => x.ToString() ?? "?").ToList();
}
