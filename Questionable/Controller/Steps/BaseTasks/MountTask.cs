using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;
using Questionable.Data;

namespace Questionable.Controller.Steps.BaseTasks;

internal sealed class MountTask(
    GameFunctions gameFunctions,
    ICondition condition,
    TerritoryData territoryData,
    ILogger<MountTask> logger) : ITask
{
    private ushort _territoryId;
    private bool _mountTriggered;

    public ITask With(ushort territoryId)
    {
        _territoryId = territoryId;
        return this;
    }

    public bool Start()
    {
        if (condition[ConditionFlag.Mounted])
            return false;

        if (!territoryData.CanUseMount(_territoryId))
        {
            logger.LogInformation("Can't use mount in current territory {Id}", _territoryId);
            return false;
        }

        if (gameFunctions.HasStatusPreventingSprintOrMount())
            return false;

        logger.LogInformation("Step wants a mount, trying to mount in territory {Id}...", _territoryId);
        if (!condition[ConditionFlag.InCombat])
        {
            _mountTriggered = gameFunctions.Mount();
            return true;
        }

        return false;
    }

    public ETaskResult Update()
    {
        if (!_mountTriggered)
        {
            _mountTriggered = gameFunctions.Mount();
            return ETaskResult.StillRunning;
        }

        return condition[ConditionFlag.Mounted]
            ? ETaskResult.TaskComplete
            : ETaskResult.StillRunning;
    }

    public override string ToString() => "Mount";
}
