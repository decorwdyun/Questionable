using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Questionable.Controller.Steps;

internal sealed class TaskQueue
{
    private readonly List<ITask> _tasks = [];
    private int _currentTaskIndex;
    public ITask? CurrentTask { get; set; }

    public IEnumerable<ITask> RemainingTasks => _tasks.Skip(_currentTaskIndex);
    public bool AllTasksComplete => CurrentTask == null && _currentTaskIndex >= _tasks.Count;

    public void Enqueue(ITask task)
    {
        _tasks.Add(task);
    }

    public bool TryDequeue([NotNullWhen(true)] out ITask? task)
    {
        if (_currentTaskIndex >= _tasks.Count)
        {
            task = null;
            return false;
        }

        task = _tasks[_currentTaskIndex];
        if (task.ShouldRedoOnInterrupt())
            _currentTaskIndex++;
        else
            _tasks.RemoveAt(0);
        return true;
    }

    public bool TryPeek([NotNullWhen(true)] out ITask? task)
    {
        if (_currentTaskIndex >= _tasks.Count)
        {
            task = null;
            return false;
        }

        task = _tasks[_currentTaskIndex];
        return true;
    }

    public void Reset()
    {
        _tasks.Clear();
        _currentTaskIndex = 0;
        CurrentTask = null;
    }

    public void InterruptWith(List<ITask> interruptionTasks)
    {
        if (CurrentTask != null)
        {
            _tasks.Insert(0, CurrentTask);
            CurrentTask = null;
            _currentTaskIndex = 0;
        }

        _tasks.InsertRange(0, interruptionTasks);
    }
}
