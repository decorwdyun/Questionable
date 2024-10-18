using System;

namespace Questionable.Controller.Steps;

internal interface ITaskExecutor
{
    ITask CurrentTask { get; }
    public InteractionProgressContext? ProgressContext { get; }

    Type GetTaskType();

    bool Start(ITask task);

    bool WasInterrupted();

    ETaskResult Update();
}

internal abstract class TaskExecutor<T> : ITaskExecutor
    where T : class, ITask
{
    protected T Task { get; set; } = null!;
    public InteractionProgressContext? ProgressContext { get; set; }
    ITask ITaskExecutor.CurrentTask => Task;

    public bool WasInterrupted()
    {
        if (ProgressContext is {} progressContext)
        {
            progressContext.Update();
            return progressContext.WasInterrupted();
        }

        return false;
    }

    public Type GetTaskType() => typeof(T);

    protected abstract bool Start();

    public bool Start(ITask task)
    {
        if (task is T t)
        {
            Task = t;
            return Start();
        }
        throw new TaskException($"Unable to cast {task.GetType()} to {typeof(T)}");
    }

    public abstract ETaskResult Update();
}
