using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Questionable.Controller.Steps;

internal sealed class TaskQueue
{
    private readonly List<ITask> _completedTasks = [];
    private readonly List<ITask> _tasks = [];
    public ITask? CurrentTask { get; set; }

    public IEnumerable<ITask> RemainingTasks => _tasks;
    public bool AllTasksComplete => CurrentTask == null && _tasks.Count == 0;

    public void Enqueue(ITask task)
    {
        _tasks.Add(task);
    }

    public bool TryDequeue([NotNullWhen(true)] out ITask? task)
    {
        task = _tasks.FirstOrDefault();
        if (task == null)
            return false;

        if (task.ShouldRedoOnInterrupt())
            _completedTasks.Add(task);

        _tasks.RemoveAt(0);
        return true;
    }

    public bool TryPeek([NotNullWhen(true)] out ITask? task)
    {
        task = _tasks.FirstOrDefault();
        return task != null;
    }

    public void Reset()
    {
        _tasks.Clear();
        _completedTasks.Clear();
        CurrentTask = null;
    }

    public void InterruptWith(List<ITask> interruptionTasks)
    {
        List<ITask?> newTasks =
        [
            ..interruptionTasks,
            .._completedTasks.Where(x => !ReferenceEquals(x, CurrentTask)).ToList(),
            CurrentTask,
            .._tasks
        ];
        Reset();
        _tasks.AddRange(newTasks.Where(x => x != null).Cast<ITask>());
    }
}
