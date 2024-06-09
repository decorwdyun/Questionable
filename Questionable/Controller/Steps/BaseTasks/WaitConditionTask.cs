using System;

namespace Questionable.Controller.Steps.BaseTasks;

internal sealed class WaitConditionTask(Func<bool> predicate, string description) : ITask
{
    public bool Start() => predicate();

    public ETaskResult Update() => predicate() ? ETaskResult.TaskComplete : ETaskResult.StillRunning;

    public override string ToString() => description;
}
