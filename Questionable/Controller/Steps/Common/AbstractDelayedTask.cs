using System;

namespace Questionable.Controller.Steps.Common;

internal abstract class AbstractDelayedTask : ITask
{
    private DateTime _continueAt;

    protected AbstractDelayedTask(TimeSpan delay)
    {
        Delay = delay;
    }

    protected TimeSpan Delay { get; set; }

    protected AbstractDelayedTask()
        : this(TimeSpan.FromSeconds(5))
    {
    }

    public virtual InteractionProgressContext? ProgressContext() => null;

    public bool Start()
    {
        _continueAt = DateTime.Now.Add(Delay);
        return StartInternal();
    }

    protected abstract bool StartInternal();

    public virtual ETaskResult Update()
    {
        if (_continueAt >= DateTime.Now)
            return ETaskResult.StillRunning;

        return UpdateInternal();
    }

    protected virtual ETaskResult UpdateInternal() => ETaskResult.TaskComplete;
}
