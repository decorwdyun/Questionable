using System;

namespace Questionable.Controller.Steps.BaseTasks;

internal sealed class WaitConditionTask(Func<bool> predicate, string description) : ITask
{
    private DateTime _continueAt = DateTime.MaxValue;

    public bool Start() => !predicate();

    public ETaskResult Update()
    {
        if (_continueAt == DateTime.MaxValue)
        {
            if (predicate())
                _continueAt = DateTime.Now.AddSeconds(0.5);
        }

        return DateTime.Now >= _continueAt ? ETaskResult.TaskComplete : ETaskResult.StillRunning;
    }

    public override string ToString() => description;
}
