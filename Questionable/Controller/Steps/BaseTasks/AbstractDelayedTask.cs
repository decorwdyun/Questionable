using System;

namespace Questionable.Controller.Steps.BaseTasks;

internal abstract class AbstractDelayedTask : ITask
{
    protected readonly TimeSpan Delay;
    private DateTime _continueAt;

    protected AbstractDelayedTask(TimeSpan delay)
    {
        Delay = delay;
    }

    protected AbstractDelayedTask()
        : this(TimeSpan.FromSeconds(5))
    {
    }

    public bool Start()
    {
        _continueAt = DateTime.Now.Add(Delay);
        return StartInternal();
    }

    protected abstract bool StartInternal();

    public ETaskResult Update()
    {
        if (_continueAt >= DateTime.Now)
            return ETaskResult.StillRunning;

        return UpdateInternal();
    }

    protected virtual ETaskResult UpdateInternal() => ETaskResult.TaskComplete;
}
