using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace Questionable.Controller.Steps.Common;

internal sealed class UnmountTask(ICondition condition, ILogger<UnmountTask> logger, GameFunctions gameFunctions)
    : ITask
{
    private bool _unmountTriggered;
    private DateTime _continueAt = DateTime.MinValue;

    public bool Start()
    {
        if (!condition[ConditionFlag.Mounted])
            return false;

        logger.LogInformation("Step explicitly wants no mount, trying to unmount...");
        if (condition[ConditionFlag.InFlight])
        {
            gameFunctions.Unmount();
            _continueAt = DateTime.Now.AddSeconds(1);
            return true;
        }

        _unmountTriggered = gameFunctions.Unmount();
        _continueAt = DateTime.Now.AddSeconds(1);
        return true;
    }

    public ETaskResult Update()
    {
        if (_continueAt >= DateTime.Now)
            return ETaskResult.StillRunning;

        if (!_unmountTriggered)
        {
            // if still flying, we still need to land
            if (condition[ConditionFlag.InFlight])
                gameFunctions.Unmount();
            else
                _unmountTriggered = gameFunctions.Unmount();

            _continueAt = DateTime.Now.AddSeconds(1);
            return ETaskResult.StillRunning;
        }

        return condition[ConditionFlag.Mounted]
            ? ETaskResult.StillRunning
            : ETaskResult.TaskComplete;
    }

    public override string ToString() => "Unmount";
}
