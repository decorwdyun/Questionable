using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace Questionable.Controller.Steps.BaseTasks;

internal sealed class UnmountTask(ICondition condition, ILogger<UnmountTask> logger, GameFunctions gameFunctions)
    : ITask
{
    private bool _unmountTriggered;
    private DateTime _unmountedAt = DateTime.MinValue;

    public bool Start()
    {
        if (!condition[ConditionFlag.Mounted])
            return false;

        logger.LogInformation("Step explicitly wants no mount, trying to unmount...");
        _unmountTriggered = gameFunctions.Unmount();
        if (_unmountTriggered)
            _unmountedAt = DateTime.Now;
        return true;
    }

    public ETaskResult Update()
    {
        if (!_unmountTriggered)
        {
            _unmountTriggered = gameFunctions.Unmount();
            if (_unmountTriggered)
                _unmountedAt = DateTime.Now;

            return ETaskResult.StillRunning;
        }

        if (DateTime.Now < _unmountedAt.AddSeconds(1))
            return ETaskResult.StillRunning;

        return condition[ConditionFlag.Mounted]
            ? ETaskResult.StillRunning
            : ETaskResult.TaskComplete;
    }

    public override string ToString() => "Unmount";
}
